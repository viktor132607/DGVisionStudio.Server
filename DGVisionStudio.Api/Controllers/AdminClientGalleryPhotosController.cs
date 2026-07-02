using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries/{galleryId:int}")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleryPhotosController : ControllerBase
{
	private const long MaxPhotoUploadSizeBytes = 20 * 1024 * 1024;
	private const long MaxPhotoUploadRequestSizeBytes = 25 * 1024 * 1024;
	private const long MaxVideoUploadSizeBytes = 100 * 1024 * 1024;
	private const long MaxVideoUploadRequestSizeBytes = 105 * 1024 * 1024;

	private readonly IClientGalleryService _clientGalleryService;
	private readonly IAuditLogService _auditLogService;
	private readonly ILogger<AdminClientGalleryPhotosController> _logger;
	private readonly AppDbContext _dbContext;
	private readonly IWebHostEnvironment _environment;
	private readonly ClientGalleryMapper _mapper;

	public AdminClientGalleryPhotosController(
		IClientGalleryService clientGalleryService,
		IAuditLogService auditLogService,
		ILogger<AdminClientGalleryPhotosController> logger,
		AppDbContext dbContext,
		IWebHostEnvironment environment,
		ClientGalleryMapper mapper)
	{
		_clientGalleryService = clientGalleryService;
		_auditLogService = auditLogService;
		_logger = logger;
		_dbContext = dbContext;
		_environment = environment;
		_mapper = mapper;
	}

	[HttpGet("photos/{photoId:int}/download")]
	public async Task<IActionResult> DownloadPhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId)
	{
		if (galleryId <= 0 || photoId <= 0)
			return BadRequest(new { message = "Invalid gallery or photo id." });

		var result = await _clientGalleryService.OpenPhotoDownloadAsync(
			galleryId,
			photoId,
			userId: string.Empty,
			isAdmin: true);

		if (result == null)
		{
			_logger.LogWarning(
				"Admin photo download failed. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
				galleryId,
				photoId,
				User.Identity?.Name,
				HttpContext.TraceIdentifier);

			return NotFound(new { message = "Photo not found." });
		}

		_logger.LogInformation(
			"Admin downloaded photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			photoId,
			result.Value.FileName,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"DownloadPhoto",
			"ClientGalleryPhoto",
			photoId.ToString(),
			null,
			new
			{
				GalleryId = galleryId,
				PhotoId = photoId,
				result.Value.FileName
			});

		return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
	}

	[RequestSizeLimit(MaxPhotoUploadRequestSizeBytes)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoUploadRequestSizeBytes)]
	[HttpPost("photos/upload")]
	public async Task<IActionResult> UploadPhoto(
		[FromRoute] int galleryId,
		IFormFile file)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (file == null || file.Length == 0)
			return BadRequest(new { message = "File is required." });

		if (file.Length > MaxPhotoUploadSizeBytes)
			return BadRequest(new { message = "Photo is too large. Maximum size is 20MB." });

		if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
			return BadRequest(new { message = "Only image files are allowed." });

		var photo = await _clientGalleryService.UploadPhotoAsync(galleryId, file);
		if (photo == null)
			return NotFound(new { message = "Gallery not found." });

		_logger.LogInformation(
			"Admin uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			photo.Id,
			file.FileName,
			file.Length,
			file.ContentType,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"UploadGalleryPhoto",
			"ClientGalleryPhoto",
			photo.Id.ToString(),
			null,
			new
			{
				GalleryId = galleryId,
				PhotoId = photo.Id,
				file.FileName,
				file.Length,
				file.ContentType
			});

		return Ok(photo);
	}

	[RequestSizeLimit(MaxVideoUploadRequestSizeBytes)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxVideoUploadRequestSizeBytes)]
	[HttpPost("videos/upload")]
	public async Task<IActionResult> UploadVideo(
		[FromRoute] int galleryId,
		IFormFile file)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (file == null || file.Length == 0)
			return BadRequest(new { message = "File is required." });

		if (file.Length > MaxVideoUploadSizeBytes)
			return BadRequest(new { message = "Video is too large. Maximum size is 100MB." });

		if (!IsAllowedVideo(file))
			return BadRequest(new { message = "Only video files are allowed: mp4, mov, webm, m4v." });

		var album = await _dbContext.PortfolioAlbums.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);
		if (album == null)
			return NotFound(new { message = "Gallery not found." });

		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var safeFileName = $"{Guid.NewGuid():N}{extension}";
		var uploadFolder = GetVideoUploadFolder();
		Directory.CreateDirectory(uploadFolder);

		var fullPath = Path.Combine(uploadFolder, safeFileName);
		await using (var stream = System.IO.File.Create(fullPath))
		{
			await file.CopyToAsync(stream);
		}

		var savedPath = $"/uploads/portfolio/videos/{safeFileName}";
		var nextDisplayOrder = await _dbContext.PortfolioImages
			.Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
		var video = new PortfolioImage
		{
			PortfolioAlbumId = galleryId,
			ImageUrl = savedPath,
			ThumbnailUrl = null,
			AltText = string.IsNullOrWhiteSpace(originalFileNameWithoutExtension) ? null : originalFileNameWithoutExtension.Trim(),
			Caption = null,
			Width = 0,
			Height = 0,
			DisplayOrder = nextDisplayOrder + 1,
			IsCover = false,
			IsPublished = true,
			IsDeleted = false,
			DeletedAtUtc = null
		};

		_dbContext.PortfolioImages.Add(video);
		await _dbContext.SaveChangesAsync();

		_logger.LogInformation(
			"Admin uploaded gallery video locally. GalleryId: {GalleryId}, MediaId: {MediaId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			video.Id,
			file.FileName,
			file.Length,
			file.ContentType,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"UploadGalleryVideo",
			"ClientGalleryPhoto",
			video.Id.ToString(),
			null,
			new
			{
				GalleryId = galleryId,
				MediaId = video.Id,
				file.FileName,
				file.Length,
				file.ContentType,
				SavedPath = savedPath
			});

		return Ok(_mapper.MapPhotoDto(video, true, galleryId));
	}

	[HttpPut("photos/{photoId:int}")]
	public async Task<IActionResult> UpdatePhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId,
		[FromBody] UpdateClientPhotoRequest request)
	{
		if (galleryId <= 0 || photoId <= 0)
			return BadRequest(new { message = "Invalid gallery or photo id." });

		if (request == null)
			return BadRequest(new { message = "Request body is required." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
		var oldPhoto = oldGallery?.Photos.FirstOrDefault(x => x.Id == photoId);

		var photo = await _clientGalleryService.UpdatePhotoAsync(galleryId, photoId, request);
		if (photo == null)
			return NotFound(new { message = "Photo not found." });

		_logger.LogInformation(
			"Admin updated gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			photoId,
			request.IsPublished,
			request.IsCover,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"UpdateGalleryPhoto",
			"ClientGalleryPhoto",
			photoId.ToString(),
			oldPhoto,
			request);

		return Ok(photo);
	}

	[HttpDelete("photos/{photoId:int}")]
	public async Task<IActionResult> DeletePhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId)
	{
		if (galleryId <= 0 || photoId <= 0)
			return BadRequest(new { message = "Invalid gallery or photo id." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
		var oldPhoto = oldGallery?.Photos.FirstOrDefault(x => x.Id == photoId);

		var deleted = await _clientGalleryService.DeletePhotoAsync(galleryId, photoId);
		if (!deleted)
			return NotFound(new { message = "Photo not found." });

		_logger.LogWarning(
			"Admin deleted gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			photoId,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"DeleteGalleryPhoto",
			"ClientGalleryPhoto",
			photoId.ToString(),
			oldPhoto,
			null);

		return Ok(new { message = "Photo deleted successfully." });
	}

	[HttpPut("cover")]
	public async Task<IActionResult> SetCoverImage(
		[FromRoute] int galleryId,
		[FromBody] SetGalleryCoverRequest request)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (request == null)
			return BadRequest(new { message = "Request body is required." });

		if (string.IsNullOrWhiteSpace(request.CoverImageUrl))
			return BadRequest(new { message = "Cover image url is required." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);

		var updated = await _clientGalleryService.SetCoverImageAsync(galleryId, request.CoverImageUrl);
		if (!updated)
			return NotFound(new { message = "Gallery or photo not found." });

		_logger.LogInformation(
			"Admin changed gallery cover. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.CoverImageUrl,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"SetGalleryCover",
			"ClientGallery",
			galleryId.ToString(),
			oldGallery,
			request);

		return Ok(new { message = "Cover image updated successfully." });
	}

	[HttpPut("photos/reorder")]
	public async Task<IActionResult> ReorderPhotos(
		[FromRoute] int galleryId,
		[FromBody] ReorderGalleryPhotosRequest request)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (request == null)
			return BadRequest(new { message = "Request body is required." });

		if (request.OrderedPhotoIds == null || request.OrderedPhotoIds.Count == 0)
			return BadRequest(new { message = "Ordered photo ids are required." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);

		var updated = await _clientGalleryService.ReorderPhotosAsync(galleryId, request.OrderedPhotoIds);
		if (!updated)
			return NotFound(new { message = "Gallery photos not found." });

		_logger.LogInformation(
			"Admin reordered gallery photos. GalleryId: {GalleryId}, PhotoCount: {PhotoCount}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.OrderedPhotoIds.Count,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"ReorderGalleryPhotos",
			"ClientGallery",
			galleryId.ToString(),
			oldGallery?.Photos.Select(x => new { x.Id, x.DisplayOrder }),
			request);

		return Ok(new { message = "Photos reordered successfully." });
	}

	private static bool IsAllowedVideo(IFormFile file)
	{
		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".mp4",
			".mov",
			".webm",
			".m4v"
		};

		return allowedExtensions.Contains(extension) && file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
	}

	private string GetVideoUploadFolder()
	{
		var webRootPath = _environment.WebRootPath;
		if (string.IsNullOrWhiteSpace(webRootPath))
		{
			webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
		}

		return Path.Combine(webRootPath, "uploads", "portfolio", "videos");
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
