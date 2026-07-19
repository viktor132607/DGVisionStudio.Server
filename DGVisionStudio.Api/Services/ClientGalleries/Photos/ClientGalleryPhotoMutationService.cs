using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoMutationService(
    AppDbContext dbContext,
    ClientGalleryMapper mapper,
    ILogger<ClientGalleryPhotoMutationService> logger)
{
    public async Task<ClientPhotoDto?> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var photo = await dbContext.PortfolioImages
            .Include(x => x.PortfolioAlbum!)
                .ThenInclude(x => x.Images)
            .FirstOrDefaultAsync(x =>
                x.Id == photoId &&
                x.PortfolioAlbumId == galleryId &&
                !x.IsDeleted &&
                !x.PortfolioAlbum!.IsDeleted);

        if (photo == null)
            return null;

        photo.AltText = string.IsNullOrWhiteSpace(request.AltText)
            ? null
            : request.AltText.Trim();
        photo.Caption = string.IsNullOrWhiteSpace(request.Caption)
            ? (string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim())
            : request.Caption.Trim();
        photo.DisplayOrder = request.DisplayOrder ?? photo.DisplayOrder;

        if (request.IsPublished.HasValue)
            photo.IsPublished = request.IsPublished.Value;

        if (request.IsCover == true && photo.PortfolioAlbum != null)
        {
            foreach (var image in photo.PortfolioAlbum.Images.Where(x => !x.IsDeleted))
                image.IsCover = image.Id == photo.Id;

            photo.PortfolioAlbum.CoverImageUrl = photo.ThumbnailUrl ?? photo.ImageUrl;
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery photo updated. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}",
            galleryId,
            photoId,
            request.IsPublished,
            request.IsCover);

        return mapper.MapPhotoDto(photo, true, galleryId);
    }

    public async Task<bool> DeletePhotoAsync(int galleryId, int photoId)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var photo = album.Images.FirstOrDefault(x => x.Id == photoId && !x.IsDeleted);
        if (photo == null)
            return false;

        var now = DateTime.UtcNow;
        var imageUrl = photo.ImageUrl;
        var thumbnailUrl = photo.ThumbnailUrl;

        photo.IsDeleted = true;
        photo.DeletedAtUtc = now;
        photo.IsPublished = false;
        photo.IsCover = false;

        if (string.Equals(album.CoverImageUrl, photo.ThumbnailUrl, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(album.CoverImageUrl, photo.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = album.Images
                .Where(x => x.Id != photoId && !x.IsDeleted)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .FirstOrDefault();

            album.CoverImageUrl = fallback?.ThumbnailUrl ?? fallback?.ImageUrl;

            foreach (var image in album.Images)
                image.IsCover = fallback != null && image.Id == fallback.Id;
        }

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

    public async Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var normalizedCoverImageUrl = NormalizeStoredImagePath(coverImageUrl);
        var matchingPhoto = album.Images.FirstOrDefault(x =>
            !x.IsDeleted &&
            (string.Equals(
                 NormalizeStoredImagePath(x.ThumbnailUrl),
                 normalizedCoverImageUrl,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 NormalizeStoredImagePath(x.ImageUrl),
                 normalizedCoverImageUrl,
                 StringComparison.OrdinalIgnoreCase)));

        if (matchingPhoto == null)
            return false;

        foreach (var image in album.Images.Where(x => !x.IsDeleted))
            image.IsCover = image.Id == matchingPhoto.Id;

        album.CoverImageUrl = matchingPhoto.ThumbnailUrl ?? matchingPhoto.ImageUrl;

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery cover image changed. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, NormalizedCoverImageUrl: {NormalizedCoverImageUrl}",
            galleryId,
            coverImageUrl,
            normalizedCoverImageUrl);

        return true;
    }

    public async Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var albumExists = await dbContext.PortfolioAlbums
            .AnyAsync(x => x.Id == galleryId && !x.IsDeleted);
        if (!albumExists)
            return false;

        var photos = await dbContext.PortfolioImages
            .Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
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

    private static string? NormalizeStoredImagePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().Replace("\\", "/");
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            trimmed = uri.AbsolutePath;

        return trimmed.TrimStart('/');
    }
}