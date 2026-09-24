using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryOrderingService(
    AppDbContext context,
    PortfolioCategoryAuditService auditService,
    ILogger<PortfolioCategoryOrderingService> logger)
{
    public async Task<ControllerServiceResult> MoveCategoryAsync(
        int id,
        MovePortfolioCategoryRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var categories = await context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var source = categories.FirstOrDefault(x => x.Id == id);
        if (source == null)
            return ControllerServiceResult.NotFound();

        var oldValue = new { source.Id, source.DisplayOrder };

        categories.Remove(source);
        var requestedDisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder;
        var targetIndex = Math.Min(requestedDisplayOrder - 1, categories.Count);
        categories.Insert(targetIndex, source);

        ApplySequentialDisplayOrder(categories);

        await context.SaveChangesAsync();
        await auditService.LogAsync(
            "MovePortfolioCategory",
            id.ToString(),
            oldValue,
            new
            {
                CategoryId = id,
                RequestedDisplayOrder = model.DisplayOrder,
                NewDisplayOrder = source.DisplayOrder
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin moved portfolio category. CategoryId: {CategoryId}, RequestedDisplayOrder: {RequestedDisplayOrder}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            model.DisplayOrder,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(categories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id));
    }

    public async Task NormalizeDisplayOrderAsync()
    {
        var categories = await context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        ApplySequentialDisplayOrder(categories);
        await context.SaveChangesAsync();
    }

    private static void ApplySequentialDisplayOrder(IReadOnlyList<DGVisionStudio.Domain.Entities.PortfolioCategory> categories)
    {
        for (var i = 0; i < categories.Count; i++)
            categories[i].DisplayOrder = i + 1;
    }
}
