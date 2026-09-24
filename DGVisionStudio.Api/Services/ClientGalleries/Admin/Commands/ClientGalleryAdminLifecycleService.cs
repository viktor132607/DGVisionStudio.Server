using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminLifecycleService(
    AppDbContext dbContext,
    ILogger<ClientGalleryAdminLifecycleService> logger)
{
    public async Task<bool> DeleteGalleryAsync(int galleryId)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var now = DateTime.UtcNow;
        var imageCount = album.Images.Count;
        var userAccessCount = album.UserAccesses.Count;
        var isUserUploaded = album.IsUserUploaded;
        var ownerUserId = album.OwnerUserId;

        album.IsDeleted = true;
        album.DeletedAtUtc = now;
        album.AllowClientAccess = false;
        album.IsPublished = false;

        foreach (var image in album.Images)
        {
            image.IsDeleted = true;
            image.DeletedAtUtc = now;
            image.IsPublished = false;
            image.IsCover = false;
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogWarning(
            "Client gallery soft deleted. GalleryId: {GalleryId}, ImageCount: {ImageCount}, UserAccessCount: {UserAccessCount}, IsUserUploaded: {IsUserUploaded}, OwnerUserId: {OwnerUserId}",
            galleryId,
            imageCount,
            userAccessCount,
            isUserUploaded,
            ownerUserId);

        return true;
    }
}
