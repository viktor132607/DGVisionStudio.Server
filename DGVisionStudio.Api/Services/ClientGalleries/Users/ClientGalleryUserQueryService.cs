using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserQueryService(
    AppDbContext dbContext,
    ClientGalleryMapper mapper)
{
    public async Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var accessAlbums = await dbContext.UserAlbumAccesses
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
                .ThenInclude(x => x.PortfolioCategory)
            .Include(x => x.PortfolioAlbum)
                .ThenInclude(x => x.OwnerUser)
            .Where(x =>
                x.UserId == userId &&
                x.PortfolioAlbum.AllowClientAccess &&
                !x.PortfolioAlbum.IsDeleted)
            .Select(x => new
            {
                Album = x.PortfolioAlbum,
                Access = x
            })
            .ToListAsync();

        var ownedAlbums = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.OwnerUser)
            .Where(x =>
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                x.OwnerUserId == userId &&
                x.AllowClientAccess &&
                !x.IsDeleted)
            .ToListAsync();

        var result = new List<MyClientGalleryDto>();
        foreach (var item in accessAlbums)
            result.Add(mapper.MapGalleryDto(item.Album, now, item.Access));

        foreach (var album in ownedAlbums)
        {
            if (result.Any(x => x.Id == album.Id))
                continue;

            result.Add(mapper.MapGalleryDto(album, now, null, isOwner: true));
        }

        return result
            .OrderByDescending(x => x.CreatedSortDate())
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    public async Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(
        int galleryId,
        string userId)
    {
        var now = DateTime.UtcNow;
        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.OwnerUser)
            .Include(x => x.Images)
            .Include(x => x.UserAccesses)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.Id == galleryId &&
                x.AllowClientAccess &&
                !x.IsDeleted);

        if (album == null)
            return null;

        var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
        var isOwner = album.GalleryType == GalleryType.ClientPrintUpload &&
            album.IsUserUploaded &&
            album.OwnerUserId == userId;

        if (!isOwner && access == null)
            return null;

        var canDownload = isOwner
            ? !mapper.IsUserGalleryExpired(album, now)
            : access != null && mapper.IsDownloadActive(access, now);

        return mapper.MapGalleryDetailsDto(album, now, access, canDownload, isOwner);
    }

    public async Task<bool> UserCanAccessGalleryAsync(
        int galleryId,
        string userId,
        bool requireDownload)
    {
        var now = DateTime.UtcNow;
        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x =>
                x.Id == galleryId &&
                x.AllowClientAccess &&
                !x.IsDeleted);

        if (album == null)
            return false;

        if (album.GalleryType == GalleryType.ClientPrintUpload &&
            album.IsUserUploaded &&
            album.OwnerUserId == userId)
        {
            return !requireDownload || !mapper.IsUserGalleryExpired(album, now);
        }

        var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
        if (access == null)
            return false;

        return requireDownload
            ? mapper.IsDownloadActive(access, now)
            : access.PreviewEnabled;
    }
}

internal static class MyClientGalleryDtoSortExtensions
{
    public static DateTime CreatedSortDate(this MyClientGalleryDto dto) =>
        dto.ExpiresAtUtc?.AddDays(-7) ?? DateTime.MinValue;
}