using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoDownloadService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ClientGalleryMapper mapper,
    ClientGalleryNamingService namingService,
    ILogger<ClientGalleryPhotoDownloadService> logger)
{
    public async Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(
        int galleryId,
        int photoId,
        string userId,
        bool isAdmin)
    {
        var now = DateTime.UtcNow;
        var photo = await dbContext.PortfolioImages
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum!)
                .ThenInclude(x => x.UserAccesses)
            .FirstOrDefaultAsync(x =>
                x.Id == photoId &&
                x.PortfolioAlbumId == galleryId &&
                !x.IsDeleted);

        if (photo == null || photo.PortfolioAlbum == null || photo.PortfolioAlbum.IsDeleted)
            return null;

        var album = photo.PortfolioAlbum;
        var allowed = isAdmin ||
            (album.GalleryType == GalleryType.ClientPrintUpload &&
             album.IsUserUploaded &&
             album.OwnerUserId == userId &&
             !mapper.IsUserGalleryExpired(album, now));

        if (!allowed)
        {
            var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
            allowed = access != null && mapper.IsDownloadActive(access, now);
        }

        if (!allowed)
        {
            logger.LogWarning(
                "Photo download denied. GalleryId: {GalleryId}, PhotoId: {PhotoId}, UserId: {UserId}, IsAdmin: {IsAdmin}, GalleryType: {GalleryType}, IsUserUploaded: {IsUserUploaded}, OwnerUserId: {OwnerUserId}",
                galleryId,
                photoId,
                userId,
                isAdmin,
                album.GalleryType,
                album.IsUserUploaded,
                album.OwnerUserId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(photo.ImageUrl))
            return null;

        var stream = await fileStorageService.OpenReadAsync(photo.ImageUrl);
        if (stream == null)
            return null;

        var extension = Path.GetExtension(photo.ImageUrl).ToLowerInvariant();
        var contentType = GetContentType(extension);
        var fileName = $"{namingService.Slugify(album.Title)}-{photo.Id}{extension}";

        logger.LogInformation(
            "Photo download opened. GalleryId: {GalleryId}, PhotoId: {PhotoId}, UserId: {UserId}, IsAdmin: {IsAdmin}, FileName: {FileName}",
            galleryId,
            photoId,
            userId,
            isAdmin,
            fileName);

        return (stream, contentType, fileName);
    }

    private static string GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
}