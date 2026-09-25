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
    private readonly PortfolioArchiveZipWriter _writer;
    private readonly PortfolioArchiveVerifier _verifier;

    [ActivatorUtilitiesConstructor]
    public PortfolioArchiveBuilder(
        PortfolioArchiveSelectionService selection,
        PortfolioArchiveZipWriter writer,
        PortfolioArchiveVerifier verifier)
    {
        _selection = selection;
        _writer = writer;
        _verifier = verifier;
    }

    public PortfolioArchiveBuilder(
        AppDbContext db,
        IFileStorageService storage)
        : this(
            new PortfolioArchiveSelectionService(db),
            new PortfolioArchiveZipWriter(
                new PortfolioArchiveNameService(),
                new PortfolioArchivePhotoWriter(storage)),
            new PortfolioArchiveVerifier())
    {
    }

    public async Task<PhysicalFileDownloadResult> BuildAsync(
        int[]? albumIds,
        Action<ArchiveProgress>? progress,
        CancellationToken token)
    {
        var selection =
            await _selection.LoadAsync(albumIds, token);
        var total =
            selection.Albums.Sum(album => album.Images.Count);
        var root =
            $"Archive({DateTime.UtcNow:yyyy-MM-dd})";
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dgvisionstudio-archive-{Guid.NewGuid():N}.zip");

        try
        {
            progress?.Invoke(
                new("writing", 0, total));

            var written = await _writer.WriteAsync(
                path,
                root,
                selection,
                completed => progress?.Invoke(
                    new("writing", completed, total)),
                token);

            progress?.Invoke(
                new(
                    "verifying",
                    written.CompletedFiles,
                    total));

            await _verifier.VerifyAsync(
                path,
                written.Manifest,
                written.DirectoryCount,
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
}
