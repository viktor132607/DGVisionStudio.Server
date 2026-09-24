using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoReorderService(
    AppDbContext dbContext,
    ILogger<ClientGalleryPhotoReorderService> logger)
{
    public async Task<bool> ReorderPhotosAsync(
        int galleryId,
        List<int> orderedPhotoIds)
    {
        ArgumentNullException.ThrowIfNull(orderedPhotoIds);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var albumExists = await dbContext.PortfolioAlbums
            .AnyAsync(x =>
                x.Id == galleryId &&
                !x.IsDeleted);

        if (!albumExists)
            return false;

        var photos = await dbContext.PortfolioImages
            .Where(x =>
                x.PortfolioAlbumId == galleryId &&
                !x.IsDeleted)
            .ToListAsync();

        if (photos.Count == 0)
            return false;

        var photoMap = photos.ToDictionary(x => x.Id);
        var order = 1;

        foreach (var photoId in orderedPhotoIds)
        {
            if (photoMap.TryGetValue(photoId, out var photo))
                photo.DisplayOrder = order++;
        }

        foreach (var remaining in photos
            .Where(x => !orderedPhotoIds.Contains(x.Id))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id))
        {
            remaining.DisplayOrder = order++;
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery photos reordered. GalleryId: {GalleryId}, PhotoCount: {PhotoCount}",
            galleryId,
            orderedPhotoIds.Count);

        return true;
    }
}
