using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminAlbumMapper
{
    public PortfolioAlbum Create(
        ClientGalleryAdminInput input,
        int categoryId,
        string slug,
        int displayOrder) =>
        new()
        {
            PortfolioCategoryId = categoryId,
            Slug = slug,
            Title = input.Title,
            TitleEn = input.TitleEn,
            Description = input.Description,
            CoverImageUrl = input.CoverImageUrl,
            DisplayOrder = displayOrder,
            IsPublished = input.IsPublic && input.IsPublished,
            AllowClientAccess = input.IsActive,
            GalleryType = input.GalleryType,
            IsUserUploaded = false,
            UserGalleryStatus = input.UserGalleryStatus,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };

    public void ApplyUpdate(
        PortfolioAlbum album,
        ClientGalleryAdminInput input,
        int categoryId)
    {
        ArgumentNullException.ThrowIfNull(album);

        album.Title = input.Title;
        album.TitleEn = input.TitleEn;
        album.Description = input.Description;
        album.CoverImageUrl = input.CoverImageUrl;
        album.AllowClientAccess = input.IsActive;
        album.IsPublished = input.IsPublic && input.IsPublished;
        album.PortfolioCategoryId = categoryId;
        album.GalleryType = input.GalleryType;
        album.UserGalleryStatus = input.UserGalleryStatus;

        if (input.GalleryType == GalleryType.Photoshoot)
        {
            album.IsUserUploaded = false;
            album.OwnerUserId = null;
            album.ExpiresAtUtc = null;
        }
    }
}
