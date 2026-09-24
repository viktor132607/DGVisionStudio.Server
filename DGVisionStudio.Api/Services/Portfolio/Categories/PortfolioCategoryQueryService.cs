using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetCategoriesAsync()
    {
        var items = await context.PortfolioCategories
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return ControllerServiceResult.Ok(items);
    }

    public async Task<ControllerServiceResult> GetCategoryByIdAsync(int id)
    {
        var entity = await context.PortfolioCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity == null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(entity);
    }
}
