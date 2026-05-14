using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleriesController : ControllerBase
{
	private readonly IClientGalleryService _clientGalleryService;
	private readonly IAuditLogService _auditLogService;
	private readonly ILogger<AdminClientGalleriesController> _logger;

	public AdminClientGalleriesController(
		IClientGalleryService clientGalleryService,
		IAuditLogService auditLogService,
		ILogger<AdminClientGalleriesController> logger)
	{
		_clientGalleryService = clientGalleryService;
		_auditLogService = auditLogService;
		_logger = logger;
	}

	[HttpGet]
	public async Task<IActionResult> GetAllGalleries()
	{
		var galleries = await _clientGalleryService.GetAllGalleriesAsync();
		return Ok(galleries);
	}

	[HttpGet("{galleryId:int}")]
	public async Task<IActionResult> GetGalleryById([FromRoute] int galleryId)
	{
		var gallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
		if (gallery == null)
			return NotFound(new { message = "Gallery not found." });

		return Ok(gallery);
	}

	[HttpGet("{galleryId:int}/photos/{photoId:int}/download")]
	public async Task<IActionResult> DownloadPhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId)
	{
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

	[HttpPost]
	public async Task<IActionResult> CreateGallery([FromBody] AdminCreateClientGalleryRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
			return BadRequest(new { message = "Title is required." });

		var galleryId = await _clientGalleryService.CreateGalleryAsync(request);

		_logger.LogInformation(
			"Admin created client gallery. GalleryId: {GalleryId}, Title: {Title}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.Title,
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
		if (string.IsNullOrWhiteSpace(request.Title))
			return BadRequest(new { message = "Title is required." });

		var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);

		var updated = await _clientGalleryService.UpdateGalleryAsync(galleryId, request);
		if (!updated)
			return NotFound(new { message = "Gallery not found." });

		_logger.LogInformation(
			"Admin updated client gallery. GalleryId: {GalleryId}, Title: {Title}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.Title,
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

	[HttpGet("{galleryId:int}/access")]
	public async Task<IActionResult> GetGalleryAccesses([FromRoute] int galleryId)
	{
		var accesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
		return Ok(accesses);
	}

	[HttpPost("{galleryId:int}/access")]
	public async Task<IActionResult> GrantAccess(
		[FromRoute] int galleryId,
		[FromBody] GrantGalleryAccessRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.UserEmail))
			return BadRequest(new { message = "User email is required." });

		var granted = await _clientGalleryService.GrantAccessAsync(galleryId, request);
		if (!granted)
			return BadRequest(new { message = "Gallery or user was not found." });

		_logger.LogInformation(
			"Admin granted gallery access. GalleryId: {GalleryId}, UserEmail: {UserEmail}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			request.UserEmail,
			request.PreviewEnabled,
			request.DownloadEnabled,
			request.DownloadExpiresAtUtc,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"GrantGalleryAccess",
			"ClientGalleryAccess",
			galleryId.ToString(),
			null,
			request);

		return Ok(new { message = "Gallery access updated successfully." });
	}

	[HttpPut("{galleryId:int}/access/{userId}")]
	public async Task<IActionResult> UpdateAccess(
		[FromRoute] int galleryId,
		[FromRoute] string userId,
		[FromBody] UpdateGalleryAccessRequest request)
	{
		if (string.IsNullOrWhiteSpace(userId))
			return BadRequest(new { message = "User id is required." });

		var oldAccesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
		var oldAccess = oldAccesses.FirstOrDefault(x => x.UserId == userId);

		var updated = await _clientGalleryService.UpdateAccessAsync(galleryId, userId, request);
		if (!updated)
			return NotFound(new { message = "Gallery access not found." });

		_logger.LogInformation(
			"Admin updated gallery access. GalleryId: {GalleryId}, UserId: {UserId}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			userId,
			request.PreviewEnabled,
			request.DownloadEnabled,
			request.DownloadExpiresAtUtc,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"UpdateGalleryAccess",
			"ClientGalleryAccess",
			$"{galleryId}:{userId}",
			oldAccess,
			request);

		return Ok(new { message = "Gallery access updated successfully." });
	}

	[HttpDelete("{galleryId:int}/access/{userId}")]
	public async Task<IActionResult> RemoveAccess(
		[FromRoute] int galleryId,
		[FromRoute] string userId)
	{
		if (string.IsNullOrWhiteSpace(userId))
			return BadRequest(new { message = "User id is required." });

		var oldAccesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
		var oldAccess = oldAccesses.FirstOrDefault(x => x.UserId == userId);

		var removed = await _clientGalleryService.RemoveAccessAsync(galleryId, userId);
		if (!removed)
			return NotFound(new { message = "Gallery access not found." });

		_logger.LogWarning(
			"Admin removed gallery access. GalleryId: {GalleryId}, UserId: {UserId}, Admin: {Admin}, TraceId: {TraceId}",
			galleryId,
			userId,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		await AuditAsync(
			"RemoveGalleryAccess",
			"ClientGalleryAccess",
			$"{galleryId}:{userId}",
			oldAccess,
			null);

		return Ok(new { message = "Gallery access removed successfully." });
	}

	[EnableRateLimiting("upload")]
	[HttpPost("{galleryId:int}/photos/upload")]
	public async Task<IActionResult> UploadPhoto(
		[FromRoute] int galleryId,
		[FromForm] IFormFile file)
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "File is required." });

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

	[HttpPut("{galleryId:int}/photos/{photoId:int}")]
	public async Task<IActionResult> UpdatePhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId,
		[FromBody] UpdateClientPhotoRequest request)
	{
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

	[HttpDelete("{galleryId:int}/photos/{photoId:int}")]
	public async Task<IActionResult> DeletePhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId)
	{
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

	[HttpPut("{galleryId:int}/cover")]
	public async Task<IActionResult> SetCoverImage(
		[FromRoute] int galleryId,
		[FromBody] SetGalleryCoverRequest request)
	{
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

	[HttpPut("{galleryId:int}/photos/reorder")]
	public async Task<IActionResult> ReorderPhotos(
		[FromRoute] int galleryId,
		[FromBody] ReorderGalleryPhotosRequest request)
	{
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