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

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"dgvisionstudio-all-albums-{Guid.NewGuid():N}.zip");
        var addedFiles = 0;
        var skippedFiles = 0;

        try
        {
            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true);
                (addedFiles, skippedFiles) = await WriteAlbumsAsync(
                    archive,
                    albums,
                    cancellationToken,
                    CompressionLevel.NoCompression);
            }

            if (addedFiles == 0)
            {
                DeleteTempFile(tempPath);
                return NoPhotos();
            }

            _logger.LogInformation(
                "Admin created all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}",
                albums.Count,
                addedFiles,
                skippedFiles);

            return ControllerServiceResult.Ok(new PhysicalFileDownloadResult(
                tempPath,
                "application/zip",
                "dgvisionstudio-all-albums.zip",
                () =>
                {
                    DeleteTempFile(tempPath);
                    return Task.CompletedTask;
                }));
        }
        catch (OperationCanceledException)
        {
            DeleteTempFile(tempPath);
            return ControllerServiceResult.NoContent();
        }
        catch (Exception ex)
        {
            DeleteTempFile(tempPath);
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
                var addedFiles = 0;
                var skippedFiles = 0;

                try
                {
                    using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
                    (addedFiles, skippedFiles) = await WriteAlbumsAsync(
                        archive,
                        albums,
                        token,
                        CompressionLevel.NoCompression);

                    if (addedFiles == 0)
                    {
                        var entry = archive.CreateEntry("README.txt", CompressionLevel.NoCompression);
                        await using var entryStream = entry.Open();
                        await using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync("No photos could be added to the archive.");
                    }

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

    private async Task<(int AddedFiles, int SkippedFiles)> WriteAlbumsAsync(
        ZipArchive archive,
        IReadOnlyCollection<PortfolioAlbum> albums,
        CancellationToken cancellationToken,
        CompressionLevel compressionLevel)
    {
        var addedFiles = 0;
        var skippedFiles = 0;

        foreach (var album in albums)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var categoryFolder = SafeZipSegment(
                album.PortfolioCategory?.Name,
                $"category-{album.PortfolioCategoryId}");
            var albumFolder = $"{album.DisplayOrder:D3}-{SafeZipSegment(album.Title, $"album-{album.Id}")}";
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

                    var entryPath = $"{categoryFolder}/{albumFolder}/{photo.DisplayOrder:D3}-{photo.Id}{GetFileExtension(photo.ImageUrl)}";
                    var entry = archive.CreateEntry(entryPath, compressionLevel);
                    await using var entryStream = entry.Open();
                    await source.CopyToAsync(entryStream, cancellationToken);
                    addedFiles++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    skippedFiles++;
                    _logger.LogWarning(
                        ex,
                        "Skipped photo while creating all albums archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}",
                        album.Id,
                        photo.Id);
                }
            }
        }

        return (addedFiles, skippedFiles);
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
}
