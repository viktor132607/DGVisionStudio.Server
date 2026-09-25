using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryDetailsMapper(
    ClientGalleryAccessPolicy accessPolicy,
    ClientGalleryPhotoMapper photoMapper)
{
    public ClientGalleryDetailsDto Map(
        PortfolioAlbum album,
        DateTime now,
        UserAlbumAccess? access,
        bool canDownload,
        bool isOwner = false,
        bool isAdminView = false)
    {
        return new ClientGalleryDetailsDto
        {
            Id = album.Id,
            Title = album.Title,
            TitleEn = album.TitleEn,
            Description = album.Description,
            CoverImageUrl = album.CoverImageUrl,
            IsActive = album.AllowClientAccess,
            IsPublic =
                album.PortfolioCategory != null &&
                album.PortfolioCategory.Key != "client-galleries",
            IsPublished = album.IsPublished,
            PortfolioCategoryId = album.PortfolioCategoryId,
            PreviewEnabled =
                isOwner ||
                access?.PreviewEnabled == true ||
                isAdminView,
            DownloadEnabled = canDownload,
            DownloadExpiresAtUtc = access?.DownloadExpiresAtUtc,
            RemainingDownloadDays = access != null
                ? accessPolicy.GetRemainingDownloadDays(access, now)
                : null,
            IsExpired =
                access != null &&
                accessPolicy.IsExpired(access, now),
            GalleryType = album.GalleryType,
            IsUserUploaded = album.IsUserUploaded,
            OwnerUserId = album.OwnerUserId,
            OwnerEmail = album.OwnerUser?.Email,
            ExpiresAtUtc = album.ExpiresAtUtc,
            RemainingLifetimeDays =
                accessPolicy.GetRemainingLifetimeDays(album, now),
            UserGalleryStatus =
                accessPolicy.GetEffectiveStatus(album, now),
            UserAccesses = album.UserAccesses
                .OrderBy(item => item.User.Email)
                .Select(item => new GalleryUserAccessDto
                {
                    UserId = item.UserId,
                    Email = item.User.Email ?? string.Empty,
                    PreviewEnabled = item.PreviewEnabled,
                    DownloadEnabled = item.DownloadEnabled,
                    DownloadExpiresAtUtc = item.DownloadExpiresAtUtc
                })
                .ToList(),
            Photos = album.Images
                .Where(image =>
                    !image.IsDeleted &&
                    (image.IsPublished || isAdminView))
                .OrderBy(image => image.DisplayOrder)
                .ThenBy(image => image.Id)
                .Select(image =>
                    photoMapper.Map(
                        image,
                        canDownload,
                        album.Id))
                .ToList()
        };
    }
}
