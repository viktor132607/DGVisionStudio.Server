using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoUpdateService(
    AppDbContext dbContext,
    ClientGalleryMapper mapper,
    ClientGalleryPhotoCoverService coverService,
    ILogger<ClientGalleryPhotoUpdateService> logger)
{
    public async Task<ClientPhotoDto?> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

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

        photo.AltText =
            NormalizeOptional(request.AltText);

        photo.Caption =
            !string.IsNullOrWhiteSpace(request.Caption)
                ? request.Caption.Trim()
                : NormalizeOptional(request.Description);

        photo.DisplayOrder =
            request.DisplayOrder ?? photo.DisplayOrder;

        if (request.IsPublished.HasValue)
            photo.IsPublished = request.IsPublished.Value;

        if (request.IsCover == true &&
            photo.PortfolioAlbum != null)
        {
            coverService.ApplyCover(
                photo.PortfolioAlbum,
                photo);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery photo updated. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}",
            galleryId,
            photoId,
            request.IsPublished,
            request.IsCover);

        return mapper.MapPhotoDto(
            photo,
            true,
            galleryId);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
