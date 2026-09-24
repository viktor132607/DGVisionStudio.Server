using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminCategoryService(
    AppDbContext dbContext,
    ClientGalleryNamingService namingService)
{
    public async Task<int> ResolveCreateCategoryAsync(ClientGalleryAdminInput input) =>
        input.IsPublic
            ? input.PortfolioCategoryId ?? await namingService.EnsureClientAlbumsCategoryAsync()
            : await namingService.EnsureClientAlbumsCategoryAsync();

    public async Task<int> ResolveUpdateCategoryAsync(
        int currentCategoryId,
        ClientGalleryAdminInput input) =>
        input.IsPublic
            ? input.PortfolioCategoryId ?? currentCategoryId
            : await namingService.EnsureClientAlbumsCategoryAsync();

    public async Task<int> GetNextDisplayOrderAsync(int categoryId)
    {
        var maxDisplayOrder = await dbContext.PortfolioAlbums
            .Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        return maxDisplayOrder + 1;
    }
}
