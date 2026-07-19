using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminClientGalleryQueryService(
    IClientGalleryService clientGalleryService,
    UserManager<ApplicationUser> userManager)
{
    public async Task<ControllerServiceResult> GetAllGalleriesAsync() =>
        ControllerServiceResult.Ok(await clientGalleryService.GetAllGalleriesAsync());

    public async Task<ControllerServiceResult> GetAvailableUsersAsync()
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new
            {
                id = x.Id,
                email = x.Email ?? x.UserName ?? string.Empty
            })
            .ToListAsync();

        return ControllerServiceResult.Ok(users);
    }

    public async Task<ControllerServiceResult> GetGalleryByIdAsync(int galleryId)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });

        var gallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        return gallery == null
            ? ControllerServiceResult.NotFound(new { message = "Gallery not found." })
            : ControllerServiceResult.Ok(gallery);
    }
}