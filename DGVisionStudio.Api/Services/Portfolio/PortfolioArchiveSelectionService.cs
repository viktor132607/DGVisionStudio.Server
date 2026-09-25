using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed record PortfolioArchiveSelection(
    IReadOnlyList<PortfolioCategory> Categories,
    IReadOnlyList<PortfolioAlbum> Albums);

public sealed class PortfolioArchiveSelectionService(AppDbContext db)
{
    public async Task<PortfolioArchiveSelection> LoadAsync(
        int[]? albumIds,
        CancellationToken token)
    {
        if (albumIds is not null &&
            (albumIds.Length == 0 || albumIds.Any(id => id <= 0)))
        {
            throw new ArchiveRequestException("Маркирай поне един валиден албум.");
        }

        var ids = albumIds?.Distinct().ToArray();
        var categories = await db.PortfolioCategories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Id)
            .ToListAsync(token);

        var categoryIds = categories
            .Select(category => category.Id)
            .ToArray();

        var query = db.PortfolioAlbums
            .AsNoTracking()
            .Include(album => album.Images)
            .Where(album =>
                !album.IsUserUploaded &&
                categoryIds.Contains(album.PortfolioCategoryId));

        if (ids is not null)
            query = query.Where(album => ids.Contains(album.Id));

        var albums = await query
            .OrderBy(album => album.DisplayOrder)
            .ThenBy(album => album.Id)
            .ToListAsync(token);

        if (ids is not null && albums.Count != ids.Length)
        {
            throw new ArchiveRequestException(
                "Някои избрани албуми липсват или са в неактивна категория. Обнови списъка или ги премести в активна категория.");
        }

        if (albums.Count == 0)
        {
            throw new ArchiveRequestException(
                "Няма албуми в активни категории за изтегляне.");
        }

        if (ids is not null)
        {
            categories = categories
                .Where(category =>
                    albums.Any(album =>
                        album.PortfolioCategoryId == category.Id))
                .ToList();
        }

        return new PortfolioArchiveSelection(categories, albums);
    }
}
