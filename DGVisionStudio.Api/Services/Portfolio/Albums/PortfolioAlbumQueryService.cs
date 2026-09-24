using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetAlbumsAsync(PagedQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Where(x => !x.IsUserUploaded)
            .OrderBy(x => x.PortfolioCategoryId)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id);

        var total = await source.CountAsync();
        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return ControllerServiceResult.Ok(new PagedResultDto<PortfolioAlbum>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            Items = items
        });
    }
}
