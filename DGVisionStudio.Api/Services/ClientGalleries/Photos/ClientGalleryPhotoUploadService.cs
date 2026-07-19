using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoUploadService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ClientGalleryMapper mapper,
    ClientGalleryUploadValidator uploadValidator,
    ILogger<ClientGalleryPhotoUploadService> logger)
{
    public async Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file)
    {
        await uploadValidator.ValidateUploadedImageAsync(file);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);
        if (album == null)
            return null;

        var safeExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
        var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);

        await using var stream = file.OpenReadStream();
        var savedPath = await fileStorageService.SaveImageAsync(
            stream,
            safeFileName,
            Path.Combine("uploads", "portfolio"),
            maxWidth: 2400,
            quality: 82,
            CancellationToken.None);

        var nextDisplayOrder = await dbContext.PortfolioImages
            .Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var hasActiveImages = await dbContext.PortfolioImages
            .AnyAsync(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted);

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
            IsCover = !hasActiveImages,
            IsPublished = true,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        dbContext.PortfolioImages.Add(photo);

        if (string.IsNullOrWhiteSpace(album.CoverImageUrl) || !hasActiveImages)
        {
            album.CoverImageUrl = savedPath;
            photo.IsCover = true;
        }

        if (album.GalleryType == GalleryType.Photoshoot &&
            album.UserGalleryStatus != UserClientGalleryStatus.PhotoshootInProgress &&
            album.UserGalleryStatus != UserClientGalleryStatus.PhotoshootReadyForPickup &&
            album.UserGalleryStatus != UserClientGalleryStatus.PhotoshootCancelled)
        {
            album.UserGalleryStatus = UserClientGalleryStatus.PhotoshootUploaded;
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin/service uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, SavedPath: {SavedPath}",
            galleryId,
            photo.Id,
            file.FileName,
            file.Length,
            file.ContentType,
            savedPath);

        return mapper.MapPhotoDto(photo, true, galleryId);
    }
}