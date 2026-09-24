using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryAlbumAssignmentService(
    AppDbContext context,
    PortfolioCategoryAuditService auditService,
    ILogger<PortfolioCategoryAlbumAssignmentService> logger)
{
    public async Task<ControllerServiceResult> GetCategoryAlbumsAsync(int id)
    {
        var category = await context.PortfolioCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (category == null)
            return ControllerServiceResult.NotFound();

        var albums = await context.PortfolioAlbums
            .AsNoTracking()
            .Where(x => !x.IsUserUploaded)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Slug,
                x.CoverImageUrl,
                x.DisplayOrder,
                x.IsPublished,
                x.PortfolioCategoryId,
                IsSelected = x.PortfolioCategoryId == id
            })
            .ToListAsync();

        return ControllerServiceResult.Ok(new { category, albums });
    }

    public async Task<ControllerServiceResult> UpdateCategoryAlbumsAsync(
        int id,
        UpdateCategoryAlbumsRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var categoryExists = await context.PortfolioCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);

        if (!categoryExists)
            return ControllerServiceResult.NotFound();

        var selectedAlbumIds = (model.AlbumIds ?? []).Distinct().ToHashSet();
        var albums = await context.PortfolioAlbums
            .Where(x => !x.IsUserUploaded)
            .ToListAsync();

        var oldValue = albums
            .Where(x => x.PortfolioCategoryId == id)
            .Select(x => new { x.Id, x.Title, x.PortfolioCategoryId })
            .ToList();

        foreach (var album in albums)
        {
            if (selectedAlbumIds.Contains(album.Id))
                album.PortfolioCategoryId = id;
        }

        await context.SaveChangesAsync();
        await auditService.LogAsync(
            "UpdatePortfolioCategoryAlbums",
            id.ToString(),
            oldValue,
            new { CategoryId = id, SelectedAlbumIds = selectedAlbumIds },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin updated category albums. CategoryId: {CategoryId}, SelectedAlbumCount: {SelectedAlbumCount}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            selectedAlbumIds.Count,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.NoContent();
    }
}
