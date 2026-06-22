using System.IO.Compression;
using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleriesController : ControllerBase
{
	private static readonly HttpClient HttpClient = new();

	private readonly IClientGalleryService _clientGalleryService;
	private readonly IAuditLogService _auditLogService;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly AppDbContext _dbContext;
	private readonly IFileStorageService _fileStorageService;
	private readonly ILogger<AdminClientGalleriesController> _logger;

	public AdminClientGalleriesController(
		IClientGalleryService clientGalleryService,
		IAuditLogService auditLogService,
		UserManager<ApplicationUser> userManager,
		AppDbContext dbContext,
		IFileStorageService fileStorageService,
		ILogger<AdminClientGalleriesController> logger)
	{
		_clientGalleryService = clientGalleryService;
		_auditLogService = auditLogService;
		_userManager = userManager;
		_dbContext = dbContext;
		_fileStorageService = fileStorageService;
		_logger = logger;
	}

	[HttpGet]
	public async Task<IActionResult> GetAllGalleries()
	{
		var galleries = await _clientGalleryService.GetAllGalleriesAsync();
		return Ok(galleries);
	}

	[HttpGet("download-all")]
	public async Task<IActionResult> DownloadAllAlbums()
	{
		try
		{
			var albums = await _dbContext.PortfolioAlbums
				.AsNoTracking()
				.Include(x => x.PortfolioCategory)
				.Include(x => x.Images)
				.Where(x => !x.IsDeleted && x.PortfolioCategory != null && !x.PortfolioCategory.IsDeleted)
				.OrderBy(x => x.PortfolioCategory!.DisplayOrder)
				.ThenBy(x => x.DisplayOrder)
				.ThenBy(x => x.Id)
				.ToListAsync();

			if (albums.Count == 0)
				return NotFound(new { message = "Няма намерени албуми." });

			var memory = new MemoryStream();
			var addedFiles = 0;
			var skippedFiles = 0;

			using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
			{
				foreach (var album in albums)
				{
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
							var source = await TryOpenPhotoStreamAsync(photo.ImageUrl, album.Id, photo.Id);
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
							_logger.LogWarning(
								ex,
								"Skipped photo while building all albums archive. AlbumId: {AlbumId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
								album.Id,
								photo.Id,
								photo.ImageUrl,
								HttpContext.TraceIdentifier);
						}
					}
				}
			}

			if (addedFiles == 0)
			{
				await memory.DisposeAsync();
				return NotFound(new { message = "Не са намерени снимки за изтегляне." });
			}

			memory.Position = 0;

			_logger.LogInformation(
				"Admin downloaded all albums archive. Albums: {AlbumCount}, Photos: {PhotoCount}, Skipped: {SkippedFiles}, Admin: {Admin}, TraceId: {TraceId}",
				albums.Count,
				addedFiles,
				skippedFiles,
				User.Identity?.Name,
				HttpContext.TraceIdentifier);

			return File(memory, "application/zip", "dgvisionstudio-all-albums.zip");
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to build all albums archive. Admin: {Admin}, TraceId: {TraceId}",
				User.Identity?.Name,
				HttpContext.TraceIdentifier);

			return StatusCode(StatusCodes.Status500InternalServerError, new
			{
				message = $"Архивът не можа да бъде създаден: {ex.Message}"
			});
		}
	}

	[HttpGet("users")]
	public async Task<IActionResult> GetAvailableUsers()
	{
		var users = await _userManager.Users
			.AsNoTracking()
			.OrderBy(x => x.Email)
			.Select(x => new
			{
				id = x.Id,
				email = x.Email ?? x.UserName ?? string.Empty
			})
			.ToListAsync();

		return Ok(users);
	}

	[HttpGet("{galleryId:int}")]
	public async Task<IActionResult> GetGalleryById([FromRoute] int galleryId)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		var gallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
		if (gallery == null)
			return NotFound(new { message = "Gallery not found." });

		return Ok(gallery);
	}

	[HttpPost]
	public async Task<IActionResult> CreateGallery([FromBody] AdminCreateClientGalleryRequest request)
	{
		if (request == null)
			return BadRequest(new { message = "Request body is required." });

		if (string.IsNullOrWhiteSpace(request.Title))
			return BadRequest(new { message = "Title is required." });

		var galleryId = await _clientGalleryService.CreateGalleryAsync(request);

		_logger.LogInformation(
			"Admin created client gallery. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, UserGalleryStatus: {UserGalleryStatus}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.Title,
			request.GalleryType,
			request.UserGalleryStatus,
			request.IsPublic,
			request.IsPublished,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"CreateGallery",
			"ClientGallery",
			galleryId.ToString(),
			null,
			request);

		return Ok(new
		{
			message = "Client gallery created successfully.",
			id = galleryId
		});
	}

	[HttpPut("{galleryId:int}")]
	public async Task<IActionResult> UpdateGallery(
		[FromRoute] int galleryId,
		[FromBody] AdminUpdateClientGalleryRequest request)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (request == null)
			return BadRequest(new { message = "Request body is required." });

		if (string.IsNullOrWhiteSpace(request.Title))
			return BadRequest(new { message = "Title is required." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);

		var updated = await _clientGalleryService.UpdateGalleryAsync(galleryId, request);
		if (!updated)
			return NotFound(new { message = "Gallery not found." });

		_logger.LogInformation(
			"Admin updated client gallery. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, UserGalleryStatus: {UserGalleryStatus}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.Title,
			request.GalleryType,
			request.UserGalleryStatus,
			request.IsPublic,
			request.IsPublished,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"UpdateGallery",
			"ClientGallery",
			galleryId.ToString(),
			oldGallery,
			request);

		return Ok(new { message = "Client gallery updated successfully." });
	}

	[HttpDelete("{galleryId:int}")]
	public async Task<IActionResult> DeleteGallery([FromRoute] int galleryId)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);

		var deleted = await _clientGalleryService.DeleteGalleryAsync(galleryId);
		if (!deleted)
			return NotFound(new { message = "Gallery not found." });

		_logger.LogWarning(
			"Admin deleted client gallery. GalleryId: {GalleryId}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"DeleteGallery",
			"ClientGallery",
			galleryId.ToString(),
			oldGallery,
			null);

		return Ok(new { message = "Client gallery deleted successfully." });
	}

	private async Task<Stream?> TryOpenPhotoStreamAsync(string imageUrl, int albumId, int photoId)
	{
		try
		{
			var storageStream = await _fileStorageService.OpenReadAsync(imageUrl);
			if (storageStream != null)
				return storageStream;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"File storage open failed for photo. AlbumId: {AlbumId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
				albumId,
				photoId,
				imageUrl,
				HttpContext.TraceIdentifier);
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
				_logger.LogWarning(
					"HTTP fallback failed for photo. AlbumId: {AlbumId}, PhotoId: {PhotoId}, StatusCode: {StatusCode}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
					albumId,
					photoId,
					(int)response.StatusCode,
					imageUrl,
					HttpContext.TraceIdentifier);

				return null;
			}

			var bytes = await response.Content.ReadAsByteArrayAsync();
			return bytes.Length > 0 ? new MemoryStream(bytes) : null;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"HTTP fallback exception for photo. AlbumId: {AlbumId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, TraceId: {TraceId}",
				albumId,
				photoId,
				imageUrl,
				HttpContext.TraceIdentifier);

			return null;
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

	private async Task AuditAsync(
		string action,
		string entityType,
		string? entityId,
		object? oldValue,
		object? newValue)
	{
		await _auditLogService.LogAsync(
			GetAdminUserId(),
			GetAdminEmail(),
			action,
			entityType,
			entityId,
			oldValue,
			newValue,
			HttpContext.Connection.RemoteIpAddress?.ToString(),
			Request.Headers.UserAgent.ToString(),
			HttpContext.TraceIdentifier);
	}

	private string GetAdminUserId()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
	}

	private string GetAdminEmail()
	{
		return User.FindFirstValue(ClaimTypes.Email)
			?? User.Identity?.Name
			?? string.Empty;
	}
}
