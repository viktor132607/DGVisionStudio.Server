using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries/{galleryId:int}")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleryPhotosController : ControllerBase
{
	private const long MaxPhotoUploadSizeBytes = 20 * 1024 * 1024;
	private const long MaxPhotoUploadRequestSizeBytes = 25 * 1024 * 1024;

	private readonly IClientGalleryService _clientGalleryService;
	private readonly IAuditLogService _auditLogService;
	private readonly ILogger<AdminClientGalleryPhotosController> _logger;

	public AdminClientGalleryPhotosController(
		IClientGalleryService clientGalleryService,
		IAuditLogService auditLogService,
		ILogger<AdminClientGalleryPhotosController> logger)
	{
		_clientGalleryService = clientGalleryService;
		_auditLogService = auditLogService;
		_logger = logger;
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
