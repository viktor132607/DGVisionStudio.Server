using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminQueryService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ClientGalleryMapper mapper)
{
    public async Task<List<MyClientGalleryDto>> GetAllGalleriesAsync()
    {
        var now = DateTime.UtcNow;
        var albums = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.OwnerUser)
            .Include(x => x.UserAccesses)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return albums.Select(album =>
        {
            var firstAccess = album.UserAccesses
                .OrderByDescending(access => access.DownloadEnabled)
                .ThenByDescending(access => access.DownloadExpiresAtUtc)
                .FirstOrDefault();

            return mapper.MapGalleryDto(
                album,
                now,
                firstAccess,
                isOwner: false,
                isAdminView: true);
        }).ToList();
    }

    public async Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId)
    {
        var now = DateTime.UtcNow;
        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.OwnerUser)
            .Include(x => x.Images)
            .Include(x => x.UserAccesses)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return null;

        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new AdminGalleryUserOptionDto
            {
                Id = x.Id,
                Email = x.Email ?? string.Empty
            })
            .ToListAsync();

        var firstDownloadAccess = album.UserAccesses
            .OrderByDescending(x => x.DownloadEnabled)
            .ThenByDescending(x => x.DownloadExpiresAtUtc)
            .FirstOrDefault();

        var canDownload = album.GalleryType != GalleryType.ClientPrintUpload ||
            !mapper.IsUserGalleryExpired(album, now);

        var dto = mapper.MapGalleryDetailsDto(
            album,
            now,
            firstDownloadAccess,
            canDownload,
            isOwner: false,
            isAdminView: true);
        dto.AvailableUsers = users;

        return dto;
    }
}