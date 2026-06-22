using System.IO.Compression;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleriesArchiveController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<AdminClientGalleriesArchiveController> _logger;

    public AdminClientGalleriesArchiveController(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminClientGalleriesArchiveController> logger)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    [HttpGet("download-all-file")]
    public async Task<IActionResult> DownloadAllAlbumsFile(CancellationToken cancellationToken)
    {
        var albums = await _dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.Images)
            .Where(x => !x.IsDeleted && x.PortfolioCategory != null && !x.PortfolioCategory.IsDeleted)
            .OrderBy(x => x.PortfolioCategory!.DisplayOrder)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (albums.Count == 0)
            return NotFound(new { message = "Няма намерени албуми." });

        var tempPath = Path.Combine(Path.GetTempPath(), $"dgvisionstudio-all-albums-{Guid.NewGuid():N}.zip");
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

                foreach (var album in albums)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var categoryFolder = SafeZipSegment(album.PortfolioCategory?.Name, $"category-{album.PortfolioCategoryId}");
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
                            if (source == null)
                            {
                                skippedFiles++;
                                continue;
                            }

                            var entryPath = $"{categoryFolder}/{albumFolder}/{photo.DisplayOrder:D3}-{photo.Id}{GetFileExtension(photo.ImageUrl)}";
                            var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);

                            await using var entryStream = entry.Open();
                            await source.CopyToAsync(entryStream, cancellationToken);
                            addedFiles++;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            skippedFiles++;
                            _logger.LogWarning(ex, "Skipped photo while creating all albums archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}", album.Id, photo.Id);
                        }
                    }
                }
            }

            if (addedFiles == 0)
            {
                DeleteTempFile(tempPath);
                return NotFound(new { message = "Не са намерени снимки за изтегляне." });
            }

            Response.OnCompleted(() =>
            {
                DeleteTempFile(tempPath);
                return Task.CompletedTask;
            });

            _logger.LogInformation(
                "Admin created all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}",
                albums.Count,
                addedFiles,
                skippedFiles);

            return PhysicalFile(tempPath, "application/zip", "dgvisionstudio-all-albums.zip");
        }
        catch (OperationCanceledException)
        {
            DeleteTempFile(tempPath);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            DeleteTempFile(tempPath);
            _logger.LogError(ex, "Failed to create all albums archive.");
            return StatusCode(500, new { message = $"Архивът не можа да бъде създаден: {ex.Message}" });
        }
    }

    private static string SafeZipSegment(string? value, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToHashSet();
        var cleaned = new string(raw.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim(' ', '.', '-');

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = fallback;

        return cleaned.Length <= 90 ? cleaned : cleaned[..90].Trim(' ', '.', '-');
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
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
