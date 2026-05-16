using DGVisionStudio.Application.DTOs.Admin;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;

	public AdminNotificationsController(AppDbContext context, UserManager<ApplicationUser> userManager)
	{
		_context = context;
		_userManager = userManager;
	}

	[HttpGet]
	public async Task<ActionResult<AdminNotificationCountsDto>> GetCounts()
	{
		var newUsers = await _userManager.Users
			.CountAsync(x => !x.IsBlocked && !x.IsSeenByAdmin);

		var newContactRequests = await _context.ContactRequests
			.CountAsync(x => !x.IsArchived && !x.IsSeenByAdmin);

		var newDirectPrintRequests = await _context.PrintRequests
			.CountAsync(x => !x.IsSeenByAdmin);

		var newUserUploadedAlbums = await _context.PortfolioAlbums
			.CountAsync(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				!x.IsSeenByAdmin &&
				!x.IsDeleted);

		return Ok(new AdminNotificationCountsDto
		{
			NewUsers = newUsers,
			NewContactRequests = newContactRequests,
			NewPrintRequests = newDirectPrintRequests + newUserUploadedAlbums
		});
	}
}