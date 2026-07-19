using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.Admin;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminStatisticsService : IAdminStatisticsService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminStatisticsService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<ControllerServiceResult> GetDashboardStatsAsync()
    {
        var users = await _userManager.Users.CountAsync();
        var newUsers = await _userManager.Users.CountAsync(x => !x.IsSeenByAdmin && !x.IsBlocked);
        var contacts = await _context.ContactRequests.CountAsync();
        var newContactRequests = await _context.ContactRequests
            .CountAsync(x => !x.IsSeenByAdmin && !x.IsArchived);
        var services = await _context.Services.CountAsync();
        var testimonials = await _context.Testimonials.CountAsync();
        var portfolioCategories = await _context.PortfolioCategories.CountAsync();
        var portfolioAlbums = await _context.PortfolioAlbums.CountAsync(x => !x.IsUserUploaded);
        var portfolioImages = await _context.PortfolioImages
            .CountAsync(x => x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded);
        var directPrintRequests = await _context.PrintRequests.CountAsync();
        var userUploadedAlbums = await _context.PortfolioAlbums.CountAsync(x => x.IsUserUploaded);
        var newDirectPrintRequests = await _context.PrintRequests.CountAsync(x => !x.IsSeenByAdmin);
        var newUserUploadedAlbums = await _context.PortfolioAlbums
            .CountAsync(x => x.IsUserUploaded && !x.IsSeenByAdmin);

        return ControllerServiceResult.Ok(new
        {
            users,
            newUsers,
            contacts,
            newContactRequests,
            services,
            testimonials,
            printRequests = directPrintRequests + userUploadedAlbums,
            newPrintRequests = newDirectPrintRequests + newUserUploadedAlbums,
            portfolioCategories,
            portfolioAlbums,
            portfolioImages
        });
    }

    public async Task<ControllerServiceResult> GetNotificationCountsAsync()
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

        return ControllerServiceResult.Ok(new AdminNotificationCountsDto
        {
            NewUsers = newUsers,
            NewContactRequests = newContactRequests,
            NewPrintRequests = newDirectPrintRequests + newUserUploadedAlbums
        });
    }
}
