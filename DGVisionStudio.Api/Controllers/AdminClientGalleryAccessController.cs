using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries/{galleryId:int}/access")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleryAccessController : ControllerBase
{
	private readonly IClientGalleryService _clientGalleryService;
	private readonly IAuditLogService _auditLogService;
	private readonly ILogger<AdminClientGalleryAccessController> _logger;

	public AdminClientGalleryAccessController(
		IClientGalleryService clientGalleryService,
		IAuditLogService auditLogService,
		ILogger<AdminClientGalleryAccessController> logger)
	{
		_clientGalleryService = clientGalleryService;
		_auditLogService = auditLogService;
		_logger = logger;
	}

	[HttpGet]
	public async Task<IActionResult> GetGalleryAccesses([FromRoute] int galleryId)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		var gallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
		if (gallery == null)
			return NotFound(new { message = "Gallery not found." });

		var accesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
		return Ok(accesses);
	}

	[HttpPost]
	public async Task<IActionResult> GrantAccess(
		[FromRoute] int galleryId,
		[FromBody] GrantGalleryAccessRequest request)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (request == null)
			return BadRequest(new { message = "Request body is required." });

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

	[HttpPut("{userId}")]
	public async Task<IActionResult> UpdateAccess(
		[FromRoute] int galleryId,
		[FromRoute] string userId,
		[FromBody] UpdateGalleryAccessRequest request)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

		if (string.IsNullOrWhiteSpace(userId))
			return BadRequest(new { message = "User id is required." });

		if (request == null)
			return BadRequest(new { message = "Request body is required." });

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

	[HttpDelete("{userId}")]
	public async Task<IActionResult> RemoveAccess(
		[FromRoute] int galleryId,
		[FromRoute] string userId)
	{
		if (galleryId <= 0)
			return BadRequest(new { message = "Invalid gallery id." });

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
