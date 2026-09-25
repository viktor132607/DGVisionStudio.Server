using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserDetailsQueryService(
    AppDbContext dbContext,
    ClientGalleryMapper mapper)
{
    public async Task<ClientGalleryDetailsDto?>
        GetGalleryDetailsAsync(
            int galleryId,
            string userId)
    {
        var now = DateTime.UtcNow;

        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(item => item.PortfolioCategory)
            .Include(item => item.OwnerUser)
            .Include(item => item.Images)
            .Include(item => item.UserAccesses)
                .ThenInclude(access => access.User)
            .FirstOrDefaultAsync(item =>
                item.Id == galleryId &&
                item.AllowClientAccess &&
                !item.IsDeleted);

        if (album == null)
            return null;

        var access = album.UserAccesses
            .FirstOrDefault(item =>
                item.UserId == userId);

        var isOwner =
            album.GalleryType ==
                GalleryType.ClientPrintUpload &&
            album.IsUserUploaded &&
            album.OwnerUserId == userId;

        if (!isOwner && access == null)
            return null;

        var canDownload = isOwner
            ? !mapper.IsUserGalleryExpired(
                album,
                now)
            : access != null &&
              mapper.IsDownloadActive(
                  access,
                  now);

        return mapper.MapGalleryDetailsDto(
            album,
            now,
            access,
            canDownload,
            isOwner);
    }
}
