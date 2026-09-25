using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserAccessQueryService(
    AppDbContext dbContext,
    ClientGalleryMapper mapper)
{
    public async Task<bool> UserCanAccessGalleryAsync(
        int galleryId,
        string userId,
        bool requireDownload)
    {
        var now = DateTime.UtcNow;

        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(item => item.UserAccesses)
            .FirstOrDefaultAsync(item =>
                item.Id == galleryId &&
                item.AllowClientAccess &&
                !item.IsDeleted);

        if (album == null)
            return false;

        if (album.GalleryType ==
                GalleryType.ClientPrintUpload &&
            album.IsUserUploaded &&
            album.OwnerUserId == userId)
        {
            return !requireDownload ||
                !mapper.IsUserGalleryExpired(
                    album,
                    now);
        }

        var access = album.UserAccesses
            .FirstOrDefault(item =>
                item.UserId == userId);

        if (access == null)
            return false;

        return requireDownload
            ? mapper.IsDownloadActive(
                access,
                now)
            : access.PreviewEnabled;
    }
}
