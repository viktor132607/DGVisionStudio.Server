using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAccessQueryService(AppDbContext dbContext)
{
    public Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId) =>
        dbContext.UserAlbumAccesses
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x =>
                x.PortfolioAlbumId == galleryId &&
                !x.PortfolioAlbum.IsDeleted)
            .OrderBy(x => x.User.Email)
            .Select(x => new GalleryUserAccessDto
            {
                UserId = x.UserId,
                Email = x.User.Email ?? string.Empty,
                PreviewEnabled = x.PreviewEnabled,
                DownloadEnabled = x.DownloadEnabled,
                DownloadExpiresAtUtc = x.DownloadExpiresAtUtc
            })
            .ToListAsync();
}
