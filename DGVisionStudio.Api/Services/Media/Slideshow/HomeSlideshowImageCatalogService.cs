using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowImageCatalogService(
    AppDbContext context)
{
    public Task<List<PortfolioImage>> GetAvailableAsync(
        bool includeScheduled = false) =>
        ApplyDefaultOrder(
                GetAvailableImagesQuery(includeScheduled)
                    .AsNoTracking())
            .ToListAsync();

    public Task<List<PortfolioImage>> GetByIdsAsync(
        IReadOnlyCollection<int> ids,
        bool includeScheduled = false) =>
        GetAvailableImagesQuery(includeScheduled)
            .AsNoTracking()
            .Where(image => ids.Contains(image.Id))
            .ToListAsync();

    public Task<List<int>> GetAvailableIdsAsync(
        IReadOnlyCollection<int> ids,
        bool includeScheduled = true) =>
        GetAvailableImagesQuery(includeScheduled)
            .AsNoTracking()
            .Where(image => ids.Contains(image.Id))
            .Select(image => image.Id)
            .ToListAsync();

    private IQueryable<PortfolioImage> GetAvailableImagesQuery(
        bool includeScheduled)
    {
        var query = context.PortfolioImages
            .Include(image => image.PortfolioAlbum!)
            .ThenInclude(album => album.PortfolioCategory)
            .Where(image =>
                image.IsPublished &&
                image.PortfolioAlbum != null &&
                image.PortfolioAlbum.IsPublished &&
                !image.PortfolioAlbum.IsUserUploaded &&
                image.PortfolioAlbum.PortfolioCategory != null &&
                image.PortfolioAlbum.PortfolioCategory.IsActive);

        if (!includeScheduled)
        {
            var now = DateTime.UtcNow;
            query = query.Where(image =>
                image.PortfolioAlbum!.PublishAtUtc == null ||
                image.PortfolioAlbum.PublishAtUtc <= now);
        }

        return query;
    }

    private static IQueryable<PortfolioImage> ApplyDefaultOrder(
        IQueryable<PortfolioImage> query) =>
        query
            .OrderBy(image =>
                image.PortfolioAlbum!
                    .PortfolioCategory!
                    .DisplayOrder)
            .ThenBy(image =>
                image.PortfolioAlbum!.DisplayOrder)
            .ThenBy(image => image.DisplayOrder)
            .ThenBy(image => image.Id);
}
