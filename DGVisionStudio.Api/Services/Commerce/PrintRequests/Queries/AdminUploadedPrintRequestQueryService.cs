using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUploadedPrintRequestQueryService(
    AppDbContext context,
    AdminPrintRequestMapper mapper)
{
    public async Task<List<PrintRequestDto>> GetAllAsync()
    {
        var albums = await context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .Include(x => x.Images)
            .Where(x =>
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted)
            .ToListAsync();

        return albums
            .Select(mapper.ToUserUploadedAlbumDto)
            .ToList();
    }

    public async Task<PrintRequestDto?> GetByAlbumIdAsync(int albumId)
    {
        var album = await context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x =>
                x.Id == albumId &&
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted);

        return album == null
            ? null
            : mapper.ToUserUploadedAlbumDto(album);
    }
}
