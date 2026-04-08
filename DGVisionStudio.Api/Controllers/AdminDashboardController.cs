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
        var contacts = await _context.ContactRequests.CountAsync();
        var services = await _context.Services.CountAsync();
        var categories = await _context.PortfolioCategories.CountAsync();
        var albums = await _context.PortfolioAlbums.CountAsync();
        var images = await _context.PortfolioImages.CountAsync();
        var testimonials = await _context.Testimonials.CountAsync();

        return Ok(new { users, contacts, services, categories, albums, images, testimonials });
    }
}
