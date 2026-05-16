using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;

	public AdminDashboardController(AppDbContext context, UserManager<ApplicationUser> userManager)
	{
		_context = context;
		_userManager = userManager;
	}

	[HttpGet]
	public async Task<IActionResult> GetStats()
	{
		var users = await _userManager.Users.CountAsync();
		var newUsers = await _userManager.Users.CountAsync(x => !x.IsSeenByAdmin && !x.IsBlocked);

		var contacts = await _context.ContactRequests.CountAsync();
		var newContactRequests = await _context.ContactRequests
			.CountAsync(x => !x.IsSeenByAdmin && !x.IsArchived);

		var services = await _context.Services.CountAsync();
		var testimonials = await _context.Testimonials.CountAsync();

		var portfolioCategories = await _context.PortfolioCategories.CountAsync();

		var portfolioAlbums = await _context.PortfolioAlbums
			.CountAsync(x => !x.IsUserUploaded);

		var portfolioImages = await _context.PortfolioImages
			.CountAsync(x => x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded);

		var directPrintRequests = await _context.PrintRequests.CountAsync();

		var userUploadedAlbums = await _context.PortfolioAlbums
			.CountAsync(x => x.IsUserUploaded);

		var printRequests = directPrintRequests + userUploadedAlbums;

		var newDirectPrintRequests = await _context.PrintRequests
			.CountAsync(x => !x.IsSeenByAdmin);

		var newUserUploadedAlbums = await _context.PortfolioAlbums
			.CountAsync(x => x.IsUserUploaded && !x.IsSeenByAdmin);

		var newPrintRequests = newDirectPrintRequests + newUserUploadedAlbums;

		return Ok(new
		{
			users,
			newUsers,

			contacts,
			newContactRequests,

			services,
			testimonials,

			printRequests,
			newPrintRequests,

			portfolioCategories,
			portfolioAlbums,
			portfolioImages
		});
	}
}