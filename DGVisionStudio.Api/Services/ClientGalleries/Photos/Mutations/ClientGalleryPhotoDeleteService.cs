using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoDeleteService(
    AppDbContext dbContext,
    ClientGalleryPhotoCoverService coverService,
    ILogger<ClientGalleryPhotoDeleteService> logger)
{
    public async Task<bool> DeletePhotoAsync(
        int galleryId,
        int photoId)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x =>
                x.Id == galleryId &&
                !x.IsDeleted);

        if (album == null)
            return false;

        var photo = album.Images.FirstOrDefault(x =>
            x.Id == photoId &&
            !x.IsDeleted);

        if (photo == null)
            return false;

        var now = DateTime.UtcNow;
        var imageUrl = photo.ImageUrl;
        var thumbnailUrl = photo.ThumbnailUrl;

        photo.IsDeleted = true;
        photo.DeletedAtUtc = now;
        photo.IsPublished = false;
        photo.IsCover = false;

        coverService.ApplyFallbackAfterDelete(
            album,
            photo);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogWarning(
            "Gallery photo soft deleted. GalleryId: {GalleryId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, ThumbnailUrl: {ThumbnailUrl}",
            galleryId,
            photoId,
            imageUrl,
            thumbnailUrl);

        return true;
    }
}
