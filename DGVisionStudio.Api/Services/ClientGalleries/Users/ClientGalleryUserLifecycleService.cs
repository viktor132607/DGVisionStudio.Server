using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserLifecycleService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<ClientGalleryUserLifecycleService> logger)
{
    public async Task<bool> DeleteUserGalleryAsync(int galleryId, string userId)
    {
        var now = DateTime.UtcNow;
        var album = await dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x =>
                x.Id == galleryId &&
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                x.OwnerUserId == userId &&
                x.AllowClientAccess &&
                !x.IsDeleted);

        if (album == null)
            return false;

        foreach (var photo in album.Images.Where(x => !x.IsDeleted).ToList())
        {
            if (!string.IsNullOrWhiteSpace(photo.ImageUrl))
            {
                try
                {
                    await fileStorageService.DeleteFileAsync(photo.ImageUrl);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to delete uploaded user gallery photo from storage. GalleryId: {GalleryId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}",
                        galleryId,
                        photo.Id,
                        photo.ImageUrl);
                }
            }

            photo.IsDeleted = true;
            photo.DeletedAtUtc = now;
            photo.IsPublished = false;
        }

        album.IsDeleted = true;
        album.DeletedAtUtc = now;
        album.IsPublished = false;
        album.AllowClientAccess = false;
        album.CoverImageUrl = null;

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "User uploaded gallery deleted by owner. GalleryId: {GalleryId}, OwnerUserId: {OwnerUserId}",
            galleryId,
            userId);

        return true;
    }
}