using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
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
	private readonly IClientGalleryService _clientGalleryService;
	private readonly IAuditLogService _auditLogService;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<AdminClientGalleriesController> _logger;

	public AdminClientGalleriesController(
		IClientGalleryService clientGalleryService,
		IAuditLogService auditLogService,
		UserManager<ApplicationUser> userManager,
		ILogger<AdminClientGalleriesController> logger)
	{
		_clientGalleryService = clientGalleryService;
		_auditLogService = auditLogService;
		_userManager = userManager;
		_logger = logger;
	}

	[HttpGet]
	public async Task<IActionResult> GetAllGalleries()
	{
		var galleries = await _clientGalleryService.GetAllGalleriesAsync();
		return Ok(galleries);
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
