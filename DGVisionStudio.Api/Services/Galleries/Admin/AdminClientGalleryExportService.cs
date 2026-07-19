using System.IO.Compression;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminClientGalleryExportService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<AdminClientGalleryExportService> logger)
{
    private static readonly HttpClient HttpClient = new();

    public async Task<ControllerServiceResult> DownloadAllAlbumsAsync(AdminRequestContext context)
    {
        try
        {
            var albums = await dbContext.PortfolioAlbums
                .AsNoTracking()
                .Include(x => x.PortfolioCategory)
                .Include(x => x.Images)
                .Where(x => !x.IsDeleted && x.PortfolioCategory != null && !x.PortfolioCategory.IsDeleted)
                .OrderBy(x => x.PortfolioCategory!.DisplayOrder)
                .ThenBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();

            if (albums.Count == 0)
                return ControllerServiceResult.NotFound(new { message = "Няма намерени албуми." });

            var memory = new MemoryStream();
            var addedFiles = 0;
            var skippedFiles = 0;

            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                foreach (var album in albums)
                {
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
                        try
                        {
                            var source = await TryOpenPhotoStreamAsync(
                                photo.ImageUrl,
                                album.Id,
                                photo.Id,
                                context.TraceId);
                            if (source == null)
                            {
                                skippedFiles++;
                                continue;
                            }

                            await using (source)
                            {
                                var ext = GetFileExtension(photo.ImageUrl);
                                var entryPath = $"{categoryFolder}/{albumFolder}/{photo.DisplayOrder:D3}-{photo.Id}{ext}";
                                var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
                                await using var entryStream = entry.Open();
                                await source.CopyToAsync(entryStream);
                                addedFiles++;
                            }
                        }
                        catch (Exception ex)
                        {
                            skippedFiles++;
                            logger.LogWarning(
                                ex,
                                "Skipped photo while building all albums archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
                                album.Id,
                                photo.Id,
                                photo.ImageUrl,
                                context.TraceId);
                        }
                    }
                }
            }

            if (addedFiles == 0)
            {
                await memory.DisposeAsync();
                return ControllerServiceResult.NotFound(new { message = "Не са намерени снимки за изтегляне." });
            }

            memory.Position = 0;
            logger.LogInformation(
                "Admin downloaded all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}, Admin: {Admin}, TraceId: {TraceId}",
                albums.Count,
                addedFiles,
                skippedFiles,
                context.DisplayName,
                context.TraceId);

            return ControllerServiceResult.Ok(new FileDownloadResult(
                memory,
                "application/zip",
                "dgvisionstudio-all-albums.zip"));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to build all albums archive. Admin: {Admin}, TraceId: {TraceId}",
                context.DisplayName,
                context.TraceId);

            return ControllerServiceResult.Error(new
            {
                message = $"Архивът не можа да бъде създаден: {ex.Message}"
            });
        }
    }

    private async Task<Stream?> TryOpenPhotoStreamAsync(
        string imageUrl,
        int albumId,
        int photoId,
        string traceId)
    {
        try
        {
            var storageStream = await fileStorageService.OpenReadAsync(imageUrl);
            if (storageStream != null)
                return storageStream;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "File storage open failed for photo. AlbumId: {AlbumId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
                albumId,
                photoId,
                imageUrl,
                traceId);
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            using var response = await HttpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HTTP fallback failed for photo. AlbumId: {AlbumId}, PhotoId: {PhotoId}, StatusCode: {StatusCode}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
                    albumId,
                    photoId,
                    (int)response.StatusCode,
                    imageUrl,
                    traceId);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return bytes.Length > 0 ? new MemoryStream(bytes) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "HTTP fallback exception for photo. AlbumId: {AlbumId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
                albumId,
                photoId,
                imageUrl,
                traceId);
            return null;
        }
    }

    private static string SafeZipSegment(string? value, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\' })
            .ToHashSet();
        var cleaned = new string(raw
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray())
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
        return string.IsNullOrWhiteSpace(ext) || ext.Length > 10
            ? ".jpg"
            : ext;
    }
}