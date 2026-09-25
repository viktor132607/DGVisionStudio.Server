using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserPhotoUploadService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ClientGalleryMapper mapper,
    ClientGalleryUploadValidator uploadValidator,
    ILogger<ClientGalleryUserCreationService> logger)
{
    public async Task<ClientPhotoDto?> UploadAsync(
        int galleryId,
        string userId,
        IFormFile file)
    {
        await uploadValidator.ValidateUploadedImageAsync(file);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
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
            return null;

        if (mapper.IsUserGalleryExpired(album, now))
        {
            logger.LogWarning(
                "User gallery photo upload rejected because gallery expired. GalleryId: {GalleryId}, OwnerUserId: {OwnerUserId}, ExpiresAtUtc: {ExpiresAtUtc}",
                galleryId,
                userId,
                album.ExpiresAtUtc);
            return null;
        }

        var safeExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
        var originalFileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(file.FileName);

        await using var stream = file.OpenReadStream();
        var savedPath = await fileStorageService.SaveImageAsync(
            stream,
            safeFileName,
            Path.Combine("uploads", "client-galleries", "originals"),
            maxWidth: 2400,
            quality: 82,
            CancellationToken.None);

        var nextDisplayOrder = album.Images
            .Where(x => !x.IsDeleted)
            .Select(x => (int?)x.DisplayOrder)
            .Max() ?? 0;

        var photo = new PortfolioImage
        {
            PortfolioAlbumId = galleryId,
            ImageUrl = savedPath,
            ThumbnailUrl = savedPath,
            AltText = string.IsNullOrWhiteSpace(originalFileNameWithoutExtension)
                ? null
                : originalFileNameWithoutExtension.Trim(),
            Caption = null,
            DisplayOrder = nextDisplayOrder + 1,
            IsCover = !album.Images.Any(x => !x.IsDeleted),
            IsPublished = true,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = now
        };

        dbContext.PortfolioImages.Add(photo);
        if (string.IsNullOrWhiteSpace(album.CoverImageUrl))
            album.CoverImageUrl = savedPath;

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "User uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, OwnerUserId: {OwnerUserId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, SavedPath: {SavedPath}",
            galleryId,
            photo.Id,
            userId,
            file.FileName,
            file.Length,
            file.ContentType,
            savedPath);

        return mapper.MapPhotoDto(photo, true, galleryId);
    }
}
