using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioImageQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetImagesAsync(PagedQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = context.PortfolioImages
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
            .Where(x =>
                x.PortfolioAlbum != null &&
                !x.PortfolioAlbum.IsUserUploaded)
            .OrderBy(x => x.PortfolioAlbumId)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id);

        var total = await source.CountAsync();
        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return ControllerServiceResult.Ok(new PagedResultDto<PortfolioImage>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            Items = items
        });
    }
}
