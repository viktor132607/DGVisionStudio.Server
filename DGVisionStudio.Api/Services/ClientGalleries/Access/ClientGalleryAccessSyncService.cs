using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAccessSyncService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager)
{
    public async Task SyncUserAccessesAsync(
        int galleryId,
        List<GalleryUserAccessDto>? requestedAccesses)
    {
        var existing = await dbContext.UserAlbumAccesses
            .Where(x => x.PortfolioAlbumId == galleryId)
            .ToListAsync();

        var desiredByUserId = await BuildDesiredAccessesAsync(
            requestedAccesses ?? []);

        foreach (var existingAccess in existing)
        {
            if (!desiredByUserId.ContainsKey(existingAccess.UserId))
                dbContext.UserAlbumAccesses.Remove(existingAccess);
        }

        foreach (var (userId, item) in desiredByUserId)
        {
            var access = existing.FirstOrDefault(x => x.UserId == userId);
            if (access == null)
            {
                access = new UserAlbumAccess
                {
                    PortfolioAlbumId = galleryId,
                    UserId = userId
                };

                dbContext.UserAlbumAccesses.Add(access);
            }

            access.PreviewEnabled = item.PreviewEnabled;
            access.DownloadEnabled = item.DownloadEnabled;
            access.DownloadExpiresAtUtc = item.DownloadExpiresAtUtc;
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<Dictionary<string, GalleryUserAccessDto>>
        BuildDesiredAccessesAsync(IEnumerable<GalleryUserAccessDto> requestedAccesses)
    {
        var desiredByUserId =
            new Dictionary<string, GalleryUserAccessDto>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in requestedAccesses)
        {
            var userId = await ResolveUserIdAsync(item);
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            desiredByUserId[userId] = item;
        }

        return desiredByUserId;
    }

    private async Task<string?> ResolveUserIdAsync(GalleryUserAccessDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.UserId))
            return item.UserId.Trim();

        if (string.IsNullOrWhiteSpace(item.Email))
            return null;

        var user = await userManager.FindByEmailAsync(item.Email.Trim());
        return user?.Id;
    }
}
