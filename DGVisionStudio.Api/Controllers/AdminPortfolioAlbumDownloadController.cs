using System.IO.Compression;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/portfolio/albums")]
[Authorize(Roles = "Admin")]
public sealed class AdminPortfolioAlbumDownloadController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<AdminPortfolioAlbumDownloadController> _logger;

    public AdminPortfolioAlbumDownloadController(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminPortfolioAlbumDownloadController> logger)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadAlbum(int id, CancellationToken cancellationToken)
    {
        var album = await _dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (album is null)
            return NotFound(new { message = "Албумът не е намерен." });

        var photos = album.Images
            .Where(x => !x.IsDeleted && !string.IsNullOrWhiteSpace(x.ImageUrl))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToList();

        if (photos.Count == 0)
            return NotFound(new { message = "В албума няма снимки за изтегляне." });

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"dgvisionstudio-album-{album.Id}-{Guid.NewGuid():N}.zip");

        try
        {
            var addedFiles = 0;

            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var archive = new ZipArchive(
                    fileStream,
                    ZipArchiveMode.Create,
                    leaveOpen: true);

                foreach (var photo in photos)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await using var source = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
                        if (source is null)
                            continue;

                        var fileName = string.IsNullOrWhiteSpace(photo.Name)
                            ? $"{photo.DisplayOrder:D3}-{photo.Id}{GetFileExtension(photo.ImageUrl)}"
                            : $"{photo.DisplayOrder:D3}-{SafeFileName(photo.Name, $"photo-{photo.Id}")}{GetFileExtension(photo.ImageUrl)}";

                        var entry = archive.CreateEntry(fileName, CompressionLevel.NoCompression);
                        await using var entryStream = entry.Open();
                        await source.CopyToAsync(entryStream, cancellationToken);
                        addedFiles++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(
                            ex,
                            "Skipped photo while creating album archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}",
                            album.Id,
                            photo.Id);
                    }
                }
            }

            if (addedFiles == 0)
            {
                DeleteTempFile(tempPath);
                return NotFound(new { message = "Не са намерени файлове за изтегляне." });
            }

            Response.Headers.CacheControl = "no-store";
            Response.OnCompleted(() =>
            {
                DeleteTempFile(tempPath);
                return Task.CompletedTask;
            });

            return PhysicalFile(
                tempPath,
                "application/zip",
                $"{SafeFileName(album.Title, $"album-{album.Id}")}.zip",
                enableRangeProcessing: false);
        }
        catch (OperationCanceledException)
        {
            DeleteTempFile(tempPath);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            DeleteTempFile(tempPath);
            _logger.LogError(ex, "Failed to create archive for portfolio album {AlbumId}", id);
            return Problem(
                title: "Неуспешно създаване на архив.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string SafeFileName(string? value, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToHashSet();
        var cleaned = new string(raw.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray())
            .Trim(' ', '.', '-');

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = fallback;

        return cleaned.Length <= 100
            ? cleaned
            : cleaned[..100].Trim(' ', '.', '-');
    }

    private static string GetFileExtension(string imageUrl)
    {
        var cleanPath = imageUrl.Split('?', '#')[0];
        var extension = Path.GetExtension(cleanPath);
        return string.IsNullOrWhiteSpace(extension) || extension.Length > 10
            ? ".jpg"
            : extension;
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
            // Best-effort cleanup.
        }
    }
}
