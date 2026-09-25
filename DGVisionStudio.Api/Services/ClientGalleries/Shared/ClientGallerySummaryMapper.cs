using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGallerySummaryMapper(
    ClientGalleryAccessPolicy accessPolicy)
{
    public MyClientGalleryDto Map(
        PortfolioAlbum album,
        DateTime now,
        UserAlbumAccess? access,
        bool isOwner = false,
        bool isAdminView = false)
    {
        var canDownload = isAdminView
            ? true
            : isOwner
                ? !accessPolicy.IsUserGalleryExpired(album, now)
                : access != null &&
                  accessPolicy.IsDownloadActive(access, now);

        return new MyClientGalleryDto
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
            PortfolioCategoryName = album.PortfolioCategory?.Name,
            PortfolioCategoryNameEn = album.PortfolioCategory?.NameEn,
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
                accessPolicy.GetEffectiveStatus(album, now)
        };
    }
}
