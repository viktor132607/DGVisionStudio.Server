using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminUpdateService(
    AppDbContext dbContext,
    IClientGalleryAccessService accessService,
    ClientGalleryNamingService namingService,
    ClientGalleryAdminInputNormalizer normalizer,
    ClientGalleryAdminCategoryService categoryService,
    ClientGalleryAdminAlbumMapper mapper,
    ILogger<ClientGalleryAdminUpdateService> logger)
{
    public async Task<bool> UpdateGalleryAsync(
        int galleryId,
        AdminUpdateClientGalleryRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var input = normalizer.Normalize(request);

        if (!string.Equals(album.Title, input.Title, StringComparison.Ordinal) ||
            !string.Equals(album.TitleEn, input.TitleEn, StringComparison.Ordinal))
        {
            album.Slug = await namingService.BuildUniqueSlugAsync(
                input.TitleEn ?? input.Title,
                galleryId);
        }

        var categoryId = await categoryService.ResolveUpdateCategoryAsync(
            album.PortfolioCategoryId,
            input);

        mapper.ApplyUpdate(album, input, categoryId);

        await dbContext.SaveChangesAsync();
        await accessService.SyncUserAccessesAsync(album.Id, input.UserAccesses);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin client gallery updated. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, Status: {Status}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, IsActive: {IsActive}",
            album.Id,
            album.Title,
            album.GalleryType,
            album.UserGalleryStatus,
            input.IsPublic,
            input.IsPublished,
            input.IsActive);

        return true;
    }
}
