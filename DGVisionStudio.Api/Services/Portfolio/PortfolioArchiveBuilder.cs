using System.IO.Compression;
using System.Text;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class ArchiveRequestException(string message) : Exception(message);
public sealed record ArchiveProgress(
    string Status,
    int CompletedFiles,
    int TotalFiles);

/// <summary>
/// Creates a complete, verified ZIP on disk.
/// A missing source invalidates the entire export.
/// </summary>
public sealed class PortfolioArchiveBuilder
{
    private readonly PortfolioArchiveSelectionService _selection;
    private readonly PortfolioArchiveNameService _names;
    private readonly PortfolioArchivePhotoWriter _photos;
    private readonly PortfolioArchiveVerifier _verifier;

    [ActivatorUtilitiesConstructor]
    public PortfolioArchiveBuilder(
        PortfolioArchiveSelectionService selection,
        PortfolioArchiveNameService names,
        PortfolioArchivePhotoWriter photos,
        PortfolioArchiveVerifier verifier)
    {
        _selection = selection;
        _names = names;
        _photos = photos;
        _verifier = verifier;
    }

    public PortfolioArchiveBuilder(
        AppDbContext db,
        IFileStorageService storage)
        : this(
            new PortfolioArchiveSelectionService(db),
            new PortfolioArchiveNameService(),
            new PortfolioArchivePhotoWriter(storage),
            new PortfolioArchiveVerifier())
    {
    }

    public async Task<PhysicalFileDownloadResult> BuildAsync(
        int[]? albumIds,
        Action<ArchiveProgress>? progress,
        CancellationToken token)
    {
        var selection = await _selection.LoadAsync(albumIds, token);
        var total = selection.Albums.Sum(album => album.Images.Count);
        var completed = 0;
        var root = $"Archive({DateTime.UtcNow:yyyy-MM-dd})";
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dgvisionstudio-archive-{Guid.NewGuid():N}.zip");

        var manifest =
            new Dictionary<string, ArchiveEntryDigest>(
                StringComparer.Ordinal);
        var directoryCount = 0;

        try
        {
            progress?.Invoke(new("writing", 0, total));

            await WriteArchiveAsync(
                path,
                root,
                selection,
                manifest,
                () => directoryCount++,
                () =>
                {
                    completed++;
                    progress?.Invoke(new(
                        "writing",
                        completed,
                        total));
                },
                token);

            progress?.Invoke(
                new("verifying", completed, total));

            await _verifier.VerifyAsync(
                path,
                manifest,
                directoryCount,
                token);

            return new(
                path,
                "application/zip",
                root + ".zip",
                () =>
                {
                    File.Delete(path);
                    return Task.CompletedTask;
                });
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    private async Task WriteArchiveAsync(
        string path,
        string root,
        PortfolioArchiveSelection selection,
        IDictionary<string, ArchiveEntryDigest> manifest,
        Action directoryCreated,
        Action fileCompleted,
        CancellationToken token)
    {
        await using var file = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            81920,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        using var zip = new ZipArchive(
            file,
            ZipArchiveMode.Create,
            leaveOpen: true,
            Encoding.UTF8);

        void DirectoryEntry(string name)
        {
            zip.CreateEntry(name + "/");
            directoryCreated();
        }

        DirectoryEntry(root);

        var categoryNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var category in selection.Categories)
        {
            token.ThrowIfCancellationRequested();

            var categoryPath =
                root + "/" + _names.CategorySegment(
                    category.Name,
                    category.Id,
                    categoryNames);

            DirectoryEntry(categoryPath);

            var albumNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var album in selection.Albums.Where(
                         album =>
                             album.PortfolioCategoryId ==
                             category.Id))
            {
                var albumPath =
                    categoryPath + "/" + _names.AlbumSegment(
                        album.Title,
                        album.Id,
                        albumNames);

                DirectoryEntry(albumPath);

                var fileNames =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                foreach (var photo in album.Images
                             .OrderBy(photo => photo.DisplayOrder)
                             .ThenBy(photo => photo.Id))
                {
                    token.ThrowIfCancellationRequested();

                    var entryPath =
                        albumPath + "/" + _names.PhotoFileName(
                            photo.Name,
                            photo.ImageUrl,
                            photo.Id,
                            fileNames);

                    var entry = zip.CreateEntry(
                        entryPath,
                        CompressionLevel.NoCompression);

                    try
                    {
                        await using var target = entry.Open();
                        manifest.Add(
                            entryPath,
                            await _photos.CopyAsync(
                                photo.ImageUrl ?? string.Empty,
                                target,
                                token));
                    }
                    catch (Exception ex) when (
                        ex is not OperationCanceledException)
                    {
                        throw new ArchiveRequestException(
                            $"Архивът не е създаден: снимка №{photo.Id} в албум „{album.Title}“ не може да бъде прочетена. Провери файла и опитай отново.");
                    }

                    fileCompleted();
                }
            }
        }
    }
}
