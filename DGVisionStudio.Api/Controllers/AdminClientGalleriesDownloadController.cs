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
public class AdminClientGalleriesDownloadController : ControllerBase
{
	private readonly AppDbContext _dbContext;
	private readonly IFileStorageService _fileStorageService;
	private readonly ILogger<AdminClientGalleriesDownloadController> _logger;

	public AdminClientGalleriesDownloadController(
		AppDbContext dbContext,
		IFileStorageService fileStorageService,
		ILogger<AdminClientGalleriesDownloadController> logger)
	{
		_dbContext = dbContext;
		_fileStorageService = fileStorageService;
		_logger = logger;
	}

	[HttpGet("download-all-stream")]
	public async Task<IActionResult> DownloadAllAlbumsStream(CancellationToken cancellationToken)
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

		var totalPhotos = albums.Sum(album => album.Images.Count(photo => !photo.IsDeleted && !string.IsNullOrWhiteSpace(photo.ImageUrl)));
		if (totalPhotos == 0)
			return NotFound(new { message = "Не са намерени снимки за изтегляне." });

		Response.ContentType = "application/zip";
		Response.Headers.ContentDisposition = "attachment; filename=\"dgvisionstudio-all-albums.zip\"";
		Response.Headers.CacheControl = "no-store";

		var addedFiles = 0;
		var skippedFiles = 0;

		try
		{
			using (var archive = new ZipArchive(Response.Body, ZipArchiveMode.Create, true))
			{
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
						try
						{
							await using var source = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
							if (source == null)
							{
								skippedFiles++;
								continue;
							}

							var ext = GetFileExtension(photo.ImageUrl);
							var entryPath = $"{categoryFolder}/{albumFolder}/{photo.DisplayOrder:D3}-{photo.Id}{ext}";
							var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);

							await using var entryStream = entry.Open();
							await source.CopyToAsync(entryStream, cancellationToken);
							addedFiles++;
						}
						catch (Exception ex) when (ex is not OperationCanceledException)
						{
							skippedFiles++;
							_logger.LogWarning(ex, "Skipped photo in all albums archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}", album.Id, photo.Id);
						}
					}
				}

				if (addedFiles == 0)
				{
					var entry = archive.CreateEntry("README.txt", CompressionLevel.NoCompression);
					await using var entryStream = entry.Open();
					await using var writer = new StreamWriter(entryStream);
					await writer.WriteAsync("No photos could be added to the archive.");
				}
			}

			_logger.LogInformation("Admin streamed all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}", albums.Count, addedFiles, skippedFiles);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Failed while streaming all albums archive.");
		}

		return new EmptyResult();
	}

	private static string SafeZipSegment(string? value, string fallback)
	{
		var raw = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
		var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToHashSet();
		var cleaned = new string(raw.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim(' ', '.', '-');
		return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned[..Math.Min(cleaned.Length, 90)].Trim(' ', '.', '-');
	}

	private static string GetFileExtension(string imageUrl)
	{
		var cleanPath = imageUrl.Split('?', '#')[0];
		var ext = Path.GetExtension(cleanPath);
		return string.IsNullOrWhiteSpace(ext) || ext.Length > 10 ? ".jpg" : ext;
	}
}
