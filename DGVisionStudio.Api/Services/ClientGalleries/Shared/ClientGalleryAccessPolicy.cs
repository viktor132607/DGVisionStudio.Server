using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAccessPolicy
{
    public bool IsDownloadActive(UserAlbumAccess access, DateTime now)
    {
        if (!access.DownloadEnabled)
            return false;

        if (!access.DownloadExpiresAtUtc.HasValue)
            return true;

        return access.DownloadExpiresAtUtc.Value >= now;
    }

    public bool IsExpired(UserAlbumAccess access, DateTime now) =>
        access.DownloadEnabled &&
        access.DownloadExpiresAtUtc.HasValue &&
        access.DownloadExpiresAtUtc.Value < now;

    public bool IsUserGalleryExpired(PortfolioAlbum album, DateTime now) =>
        album.GalleryType == GalleryType.ClientPrintUpload &&
        album.IsUserUploaded &&
        album.ExpiresAtUtc.HasValue &&
        album.ExpiresAtUtc.Value < now;

    public UserClientGalleryStatus GetEffectiveStatus(
        PortfolioAlbum album,
        DateTime now)
    {
        if (album.GalleryType == GalleryType.ClientPrintUpload &&
            IsUserGalleryExpired(album, now))
        {
            return UserClientGalleryStatus.Expired;
        }

        return album.UserGalleryStatus;
    }

    public int? GetRemainingDownloadDays(
        UserAlbumAccess access,
        DateTime now)
    {
        if (!access.DownloadEnabled ||
            !access.DownloadExpiresAtUtc.HasValue)
        {
            return null;
        }

        return Math.Max(
            0,
            (access.DownloadExpiresAtUtc.Value.Date - now.Date).Days);
    }

    public int? GetRemainingLifetimeDays(
        PortfolioAlbum album,
        DateTime now)
    {
        if (album.GalleryType != GalleryType.ClientPrintUpload ||
            !album.ExpiresAtUtc.HasValue)
        {
            return null;
        }

        return Math.Max(
            0,
            (album.ExpiresAtUtc.Value.Date - now.Date).Days);
    }
}
