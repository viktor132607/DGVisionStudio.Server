using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserListQueryService(
    AppDbContext dbContext,
    ClientGalleryMapper mapper)
{
    public async Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(
        string userId)
    {
        var now = DateTime.UtcNow;

        var accessAlbums = await dbContext.UserAlbumAccesses
            .AsNoTracking()
            .Include(access => access.PortfolioAlbum)
                .ThenInclude(album => album.PortfolioCategory)
            .Include(access => access.PortfolioAlbum)
                .ThenInclude(album => album.OwnerUser)
            .Where(access =>
                access.UserId == userId &&
                access.PortfolioAlbum.AllowClientAccess &&
                !access.PortfolioAlbum.IsDeleted)
            .Select(access => new
            {
                Album = access.PortfolioAlbum,
                Access = access
            })
            .ToListAsync();

        var ownedAlbums = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(album => album.PortfolioCategory)
            .Include(album => album.OwnerUser)
            .Where(album =>
                album.GalleryType ==
                    GalleryType.ClientPrintUpload &&
                album.IsUserUploaded &&
                album.OwnerUserId == userId &&
                album.AllowClientAccess &&
                !album.IsDeleted)
            .ToListAsync();

        var result = new List<MyClientGalleryDto>();

        foreach (var item in accessAlbums)
        {
            result.Add(
                mapper.MapGalleryDto(
                    item.Album,
                    now,
                    item.Access));
        }

        foreach (var album in ownedAlbums)
        {
            if (result.Any(item => item.Id == album.Id))
                continue;

            result.Add(
                mapper.MapGalleryDto(
                    album,
                    now,
                    null,
                    isOwner: true));
        }

        return result
            .OrderByDescending(
                item => item.CreatedSortDate())
            .ThenByDescending(item => item.Id)
            .ToList();
    }
}

internal static class MyClientGalleryDtoSortExtensions
{
    public static DateTime CreatedSortDate(
        this MyClientGalleryDto dto) =>
        dto.ExpiresAtUtc?.AddDays(-7) ??
        DateTime.MinValue;
}
