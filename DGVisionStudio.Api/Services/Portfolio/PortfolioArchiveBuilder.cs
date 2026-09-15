using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ArchiveRequestException(string message) : Exception(message);
public sealed record ArchiveProgress(string Status, int CompletedFiles, int TotalFiles);

/// <summary>Creates a complete, verified ZIP on disk. A missing source invalidates the entire export.</summary>
public sealed class PortfolioArchiveBuilder(AppDbContext db, IFileStorageService storage)
{
    private static readonly HttpClient StaticFiles = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<PhysicalFileDownloadResult> BuildAsync(
        int[]? albumIds, Action<ArchiveProgress>? progress, CancellationToken token)
    {
        if (albumIds is not null && (albumIds.Length == 0 || albumIds.Any(id => id <= 0)))
            throw new ArchiveRequestException("Маркирай поне един валиден албум.");

        var ids = albumIds?.Distinct().ToArray();
        var categories = await db.PortfolioCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync(token);
        var categoryIds = categories.Select(c => c.Id).ToArray();
        var query = db.PortfolioAlbums.AsNoTracking().Include(a => a.Images)
            .Where(a => !a.IsUserUploaded && categoryIds.Contains(a.PortfolioCategoryId));
        if (ids is not null) query = query.Where(a => ids.Contains(a.Id));
        var albums = await query.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id).ToListAsync(token);
        if (ids is not null && albums.Count != ids.Length)
            throw new ArchiveRequestException("Някои избрани албуми липсват или са в неактивна категория. Обнови списъка или ги премести в активна категория.");
        if (albums.Count == 0)
            throw new ArchiveRequestException("Няма албуми в активни категории за изтегляне.");
        if (ids is not null) categories = categories.Where(c => albums.Any(a => a.PortfolioCategoryId == c.Id)).ToList();

        var total = albums.Sum(a => a.Images.Count);
        var completed = 0;
        var root = $"Archive({DateTime.UtcNow:yyyy-MM-dd})";
        var path = Path.Combine(Path.GetTempPath(), $"dgvisionstudio-archive-{Guid.NewGuid():N}.zip");
        var manifest = new Dictionary<string, (long Length, byte[] Hash)>(StringComparer.Ordinal);
        var directoryCount = 0;
        try
        {
            progress?.Invoke(new("writing", 0, total));
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
                void DirectoryEntry(string name) { zip.CreateEntry(name + "/"); directoryCount++; }
                DirectoryEntry(root);
                var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var category in categories)
                {
                    token.ThrowIfCancellationRequested();
                    var categoryPath = root + "/" + Unique(SafeSegment(category.Name, $"category-{category.Id}", 60), category.Id, categoryNames);
                    DirectoryEntry(categoryPath);
                    var albumNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var album in albums.Where(a => a.PortfolioCategoryId == category.Id))
                    {
                        var albumPath = categoryPath + "/" + Unique(SafeSegment(album.Title, $"album-{album.Id}", 70), album.Id, albumNames);
                        DirectoryEntry(albumPath);
                        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var photo in album.Images.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id))
                        {
                            token.ThrowIfCancellationRequested();
                            var extension = Path.GetExtension((photo.ImageUrl ?? "").Split('?', '#')[0]);
                            if (extension.Length is < 2 or > 10 || extension.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c))) extension = ".jpg";
                            var name = SafeSegment(photo.Name, $"photo-{photo.Id}", 90);
                            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) name = name[..^extension.Length];
                            var entryPath = albumPath + "/" + Unique(name, photo.Id, fileNames, extension);
                            var entry = zip.CreateEntry(entryPath, CompressionLevel.NoCompression);
                            try
                            {
                                await using var target = entry.Open();
                                manifest.Add(entryPath, await CopyPhotoAsync(photo.ImageUrl ?? "", target, token));
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                throw new ArchiveRequestException($"Архивът не е създаден: снимка №{photo.Id} в албум „{album.Title}“ не може да бъде прочетена. Провери файла и опитай отново.");
                            }
                            progress?.Invoke(new("writing", ++completed, total));
                        }
                    }
                }
            }

            progress?.Invoke(new("verifying", completed, total));
            using (var zip = ZipFile.OpenRead(path))
            {
                if (zip.Entries.Count != manifest.Count + directoryCount)
                    throw new IOException("Archive entry count mismatch.");
                foreach (var (name, expected) in manifest)
                {
                    token.ThrowIfCancellationRequested();
                    var entry = zip.GetEntry(name) ?? throw new IOException("Missing ZIP entry.");
                    await using var content = entry.Open();
                    if (entry.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(
                        await SHA256.HashDataAsync(content, token), expected.Hash))
                        throw new IOException("Archive verification failed.");
                }
            }
            return new(path, "application/zip", root + ".zip", () => { File.Delete(path); return Task.CompletedTask; });
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    private async Task<(long Length, byte[] Hash)> CopyPhotoAsync(string url, Stream target, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new IOException("Missing source URL.");
        Stream? source = null;
        try { source = await storage.OpenReadAsync(url, token); }
        catch (Exception ex) when (ex is not OperationCanceledException && url.StartsWith("/images/", StringComparison.Ordinal)) { }
        token.ThrowIfCancellationRequested();
        if (source is not null)
        {
            await using (source) return await CopyAndHashAsync(source, target, token);
        }

        // Seeded portfolio images are hosted with the frontend, not in Cloudinary or API storage.
        // Only this fixed public origin and path are allowed; never follow arbitrary redirects.
        if (!url.StartsWith("/images/", StringComparison.Ordinal)) throw new IOException("Source not found.");
        var uri = new Uri(new Uri("https://dgvisionstudio.com"), url);
        if (uri.Host != "dgvisionstudio.com" || !uri.AbsolutePath.StartsWith("/images/", StringComparison.Ordinal))
            throw new IOException("Invalid static image path.");
        using var response = await StaticFiles.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType is "text/html" or "application/json")
            throw new IOException("The image URL returned an error page.");
        await using var remote = await response.Content.ReadAsStreamAsync(token);
        return await CopyAndHashAsync(remote, target, token);
    }

    private static async Task<(long Length, byte[] Hash)> CopyAndHashAsync(Stream source, Stream target, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, token)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, count), token);
            hash.AppendData(buffer, 0, count);
            length += count;
        }
        if (length == 0) throw new IOException("Empty source file.");
        return (length, hash.GetHashAndReset());
    }

    private static string SafeSegment(string? value, string fallback, int maxLength)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Normalize(NormalizationForm.FormC);
        var cleaned = new string(raw.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '-' : c).ToArray()).Trim(' ', '.');
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength].TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(cleaned)) return fallback;
        var stem = cleaned.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
            cleaned = "_" + cleaned;
        return cleaned;
    }

    private static string Unique(string name, int id, ISet<string> used, string extension = "")
    {
        var candidate = name + extension;
        var suffix = 0;
        while (!used.Add(candidate)) candidate = $"{name}-{id}{(suffix++ == 0 ? "" : $"-{suffix}")}{extension}";
        return candidate;
    }
}
