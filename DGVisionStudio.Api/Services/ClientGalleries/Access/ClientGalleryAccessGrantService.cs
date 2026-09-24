using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAccessGrantService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<ClientGalleryAccessGrantService> logger)
{
    public async Task<bool> GrantAccessAsync(
        int galleryId,
        GrantGalleryAccessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var user = await userManager.FindByEmailAsync(request.UserEmail.Trim());
        if (user == null)
            return false;

        album.AllowClientAccess = true;

        var access = await dbContext.UserAlbumAccesses
            .FirstOrDefaultAsync(x =>
                x.PortfolioAlbumId == galleryId &&
                x.UserId == user.Id);

        if (access == null)
        {
            access = new UserAlbumAccess
            {
                PortfolioAlbumId = galleryId,
                UserId = user.Id
            };

            dbContext.UserAlbumAccesses.Add(access);
        }

        ApplyPermissions(
            access,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery access granted or updated. GalleryId: {GalleryId}, UserId: {UserId}, UserEmail: {UserEmail}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}",
            galleryId,
            user.Id,
            user.Email,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc);

        return true;
    }

    private static void ApplyPermissions(
        UserAlbumAccess access,
        bool previewEnabled,
        bool downloadEnabled,
        DateTime? downloadExpiresAtUtc)
    {
        access.PreviewEnabled = previewEnabled;
        access.DownloadEnabled = downloadEnabled;
        access.DownloadExpiresAtUtc = downloadExpiresAtUtc;
    }
}
