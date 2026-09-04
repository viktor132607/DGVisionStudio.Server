using System.IO.Compression;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryArchiveService : IAdminGalleryArchiveService
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<AdminGalleryArchiveService> _logger;

    public AdminGalleryArchiveService(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminGalleryArchiveService> logger)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<ControllerServiceResult> CreatePhysicalArchiveAsync(
        CancellationToken cancellationToken)
    {
        var albums = await LoadAlbumsAsync(cancellationToken);
        if (albums.Count == 0)
            return NoAlbums();

        var archiveId = Guid.NewGuid().ToString("N");
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"dgvisionstudio-all-albums-{archiveId}");
        var tempZipPath = Path.Combine(
            Path.GetTempPath(),
            $"dgvisionstudio-all-albums-{archiveId}.zip");

        try
        {
            Directory.CreateDirectory(tempRoot);

            var (addedFiles, skippedFiles) = await WriteAlbumFoldersAsync(
                tempRoot,
                albums,
                cancellationToken);

            if (addedFiles == 0)
            {
                DeleteDirectory(tempRoot);
                DeleteTempFile(tempZipPath);
                return NoPhotos();
            }

            cancellationToken.ThrowIfCancellationRequested();

            ZipFile.CreateFromDirectory(
                tempRoot,
                tempZipPath,
                CompressionLevel.NoCompression,
                includeBaseDirectory: false);

            DeleteDirectory(tempRoot);

            _logger.LogInformation(
                "Admin created all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}",
                albums.Count,
                addedFiles,
                skippedFiles);

            return ControllerServiceResult.Ok(new PhysicalFileDownloadResult(
                tempZipPath,
                "application/zip",
                "dgvisionstudio-all-albums.zip",
                () =>
                {
                    DeleteTempFile(tempZipPath);
                    DeleteDirectory(tempRoot);
                    return Task.CompletedTask;
                }));
        }
        catch (OperationCanceledException)
        {
            DeleteDirectory(tempRoot);
            DeleteTempFile(tempZipPath);
            return ControllerServiceResult.NoContent();
        }
        catch (Exception ex)
        {
            DeleteDirectory(tempRoot);
            DeleteTempFile(tempZipPath);
            _logger.LogError(ex, "Failed to create all albums archive.");
            return ControllerServiceResult.Error(new
            {
                message = $"Архивът не можа да бъде създаден: {ex.Message}"
            });
        }
    }

    public async Task<ControllerServiceResult> PrepareStreamingArchiveAsync(
        CancellationToken cancellationToken)
    {
        var albums = await LoadAlbumsAsync(cancellationToken);
        if (albums.Count == 0)
            return NoAlbums();

        var totalPhotos = albums.Sum(album => album.Images.Count(photo =>
            !photo.IsDeleted && !string.IsNullOrWhiteSpace(photo.ImageUrl)));
        if (totalPhotos == 0)
            return NoPhotos();

        return ControllerServiceResult.Ok(new StreamingFileDownloadResult(
            "application/zip",
            "dgvisionstudio-all-albums.zip",
            async (destination, token) =>
            {
                var tempRoot = Path.Combine(
                    Path.GetTempPath(),
                    $"dgvisionstudio-all-albums-stream-{Guid.NewGuid():N}");

                try
                {
                    Directory.CreateDirectory(tempRoot);
                    var (addedFiles, skippedFiles) = await WriteAlbumFoldersAsync(
                        tempRoot,
                        albums,
                        token);

                    if (addedFiles == 0)
                        return;

                    using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
                    AddDirectoryToArchive(archive, tempRoot);

                    _logger.LogInformation(
                        "Admin streamed all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}",
                        albums.Count,
                        addedFiles,
                        skippedFiles);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed while streaming all albums archive.");
                }
                finally
                {
                    DeleteDirectory(tempRoot);
                }
            }));
    }

    private async Task<List<PortfolioAlbum>> LoadAlbumsAsync(CancellationToken cancellationToken) =>
        await _dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.Images)
            .Where(x =>
                !x.IsDeleted &&
                x.PortfolioCategory != null &&
                !x.PortfolioCategory.IsDeleted)
            .OrderBy(x => x.PortfolioCategory!.DisplayOrder)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    private async Task<(int AddedFiles, int SkippedFiles)> WriteAlbumFoldersAsync(
        string rootDirectory,
        IReadOnlyCollection<PortfolioAlbum> albums,
        CancellationToken cancellationToken)
    {
        var addedFiles = 0;
        var skippedFiles = 0;
        var usedAlbumFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var album in albums)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var albumFolderName = MakeUniqueFolderName(
                SafeZipSegment(album.Title, $"album-{album.Id}"),
                album.Id,
                usedAlbumFolderNames);
            var albumDirectory = Path.Combine(rootDirectory, albumFolderName);
            Directory.CreateDirectory(albumDirectory);

            var photos = album.Images
                .Where(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.ImageUrl))
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .ToList();

            foreach (var photo in photos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var source = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
                    if (source is null)
                    {
                        skippedFiles++;
                        continue;
                    }

                    var extension = GetFileExtension(photo.ImageUrl);
                    var baseName = SafeZipSegment(photo.Name, $"photo-{photo.Id}");
                    var fileName = $"{photo.DisplayOrder:D3}-{baseName}-{photo.Id}{extension}";
                    var destinationPath = Path.Combine(albumDirectory, fileName);

                    await using var target = new FileStream(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        1024 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);

                    await source.CopyToAsync(target, 1024 * 1024, cancellationToken);
                    addedFiles++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    skippedFiles++;
                    _logger.LogWarning(
                        ex,
                        "Skipped photo while preparing all albums archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}",
                        album.Id,
                        photo.Id);
                }
            }
        }

        return (addedFiles, skippedFiles);
    }

    private static void AddDirectoryToArchive(ZipArchive archive, string rootDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(rootDirectory, directory)
                .Replace('\\', '/');

            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                archive.CreateEntry($"{relativeDirectory.TrimEnd('/')}/");
        }

        foreach (var file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(rootDirectory, file)
                .Replace('\\', '/');
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.NoCompression);
        }
    }

    private static string MakeUniqueFolderName(
        string desiredName,
        int albumId,
        ISet<string> usedNames)
    {
        if (usedNames.Add(desiredName))
            return desiredName;

        var withId = $"{desiredName}-{albumId}";
        usedNames.Add(withId);
        return withId;
    }

    private static ControllerServiceResult NoAlbums() =>
        ControllerServiceResult.NotFound(new { message = "Няма намерени албуми." });

    private static ControllerServiceResult NoPhotos() =>
        ControllerServiceResult.NotFound(new { message = "Не са намерени снимки за изтегляне." });

    private static string SafeZipSegment(string? value, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToHashSet();
        var cleaned = new string(raw.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray())
            .Trim(' ', '.', '-');

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = fallback;

        return cleaned.Length <= 90
            ? cleaned
            : cleaned[..90].Trim(' ', '.', '-');
    }

    private static string GetFileExtension(string imageUrl)
    {
        var cleanPath = imageUrl.Split('?', '#')[0];
        var ext = Path.GetExtension(cleanPath);
        return string.IsNullOrWhiteSpace(ext) || ext.Length > 10 ? ".jpg" : ext;
    }

    private static void DeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
