using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminCreateService(
    AppDbContext dbContext,
    IClientGalleryAccessService accessService,
    ClientGalleryNamingService namingService,
    ClientGalleryAdminInputNormalizer normalizer,
    ClientGalleryAdminCategoryService categoryService,
    ClientGalleryAdminAlbumMapper mapper,
    ILogger<ClientGalleryAdminCreateService> logger)
{
    public async Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var input = normalizer.Normalize(request);
        var categoryId = await categoryService.ResolveCreateCategoryAsync(input);
        var displayOrder = await categoryService.GetNextDisplayOrderAsync(categoryId);
        var slug = await namingService.BuildUniqueSlugAsync(input.TitleEn ?? input.Title);

        var album = mapper.Create(input, categoryId, slug, displayOrder);

        dbContext.PortfolioAlbums.Add(album);
        await dbContext.SaveChangesAsync();

        await accessService.SyncUserAccessesAsync(album.Id, input.UserAccesses);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin client gallery created. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, Status: {Status}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, IsActive: {IsActive}",
            album.Id,
            album.Title,
            album.GalleryType,
            album.UserGalleryStatus,
            input.IsPublic,
            input.IsPublished,
            input.IsActive);

        return album.Id;
    }
}
