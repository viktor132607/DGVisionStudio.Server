using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoCoverService(
    AppDbContext dbContext,
    ILogger<ClientGalleryPhotoCoverService> logger)
{
    public async Task<bool> SetCoverImageAsync(
        int galleryId,
        string coverImageUrl)
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

        var normalizedCoverImageUrl =
            NormalizeStoredImagePath(coverImageUrl);

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

        ApplyCover(album, matchingPhoto);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery cover image changed. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, NormalizedCoverImageUrl: {NormalizedCoverImageUrl}",
            galleryId,
            coverImageUrl,
            normalizedCoverImageUrl);

        return true;
    }

    public void ApplyCover(
        PortfolioAlbum album,
        PortfolioImage photo)
    {
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(photo);

        foreach (var image in album.Images.Where(x => !x.IsDeleted))
            image.IsCover = image.Id == photo.Id;

        album.CoverImageUrl =
            photo.ThumbnailUrl ?? photo.ImageUrl;
    }

    public void ApplyFallbackAfterDelete(
        PortfolioAlbum album,
        PortfolioImage deletedPhoto)
    {
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(deletedPhoto);

        if (!string.Equals(
                album.CoverImageUrl,
                deletedPhoto.ThumbnailUrl,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                album.CoverImageUrl,
                deletedPhoto.ImageUrl,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fallback = album.Images
            .Where(x =>
                x.Id != deletedPhoto.Id &&
                !x.IsDeleted)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .FirstOrDefault();

        album.CoverImageUrl =
            fallback?.ThumbnailUrl ?? fallback?.ImageUrl;

        foreach (var image in album.Images)
        {
            image.IsCover =
                fallback != null &&
                image.Id == fallback.Id;
        }
    }

    public static string? NormalizeStoredImagePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().Replace("\\", "/");

        if (Uri.TryCreate(
                trimmed,
                UriKind.Absolute,
                out var uri))
        {
            trimmed = uri.AbsolutePath;
        }

        return trimmed.TrimStart('/');
    }
}
