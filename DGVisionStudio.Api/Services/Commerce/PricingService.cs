using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public interface IPricingService
{
    Task<IReadOnlyList<PricingItemResponse>> GetActiveAsync();
    Task<IReadOnlyList<PricingItemResponse>> GetAllAsync();
    Task<PricingItemResponse> CreateAsync(PricingItemRequest request);
    Task<PricingItemResponse?> UpdateAsync(int id, PricingItemRequest request);
    Task<IReadOnlyList<PricingItemResponse>> ReorderAsync(ReorderPricingItemsRequest request);
    Task<bool> DeleteAsync(int id);
}

public sealed class PricingService(AppDbContext context) : IPricingService
{
    public async Task<IReadOnlyList<PricingItemResponse>> GetActiveAsync()
    {
        var items = await context.PricingItems
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return items.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<PricingItemResponse>> GetAllAsync()
    {
        var items = await context.PricingItems
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return items.Select(ToResponse).ToList();
    }

    public async Task<PricingItemResponse> CreateAsync(PricingItemRequest request)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            throw new PricingValidationException(validationError);

        var maxOrder = await context.PricingItems
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var pricingMode = NormalizePricingMode(request.PricingMode);
        var priceText = pricingMode == "Negotiable" ? null : Normalize(request.PriceText);

        var item = new PricingItem
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            PricingMode = pricingMode,
            PriceText = priceText,
            DisplayOrder = maxOrder + 1,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.PricingItems.Add(item);
        await context.SaveChangesAsync();

        return ToResponse(item);
    }

    public async Task<PricingItemResponse?> UpdateAsync(int id, PricingItemRequest request)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            throw new PricingValidationException(validationError);

        var item = await context.PricingItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return null;

        var pricingMode = NormalizePricingMode(request.PricingMode);
        var priceText = pricingMode == "Negotiable" ? null : Normalize(request.PriceText);

        item.Title = request.Title.Trim();
        item.Description = request.Description.Trim();
        item.PricingMode = pricingMode;
        item.PriceText = priceText;
        item.IsActive = request.IsActive;

        await context.SaveChangesAsync();

        return ToResponse(item);
    }

    public async Task<IReadOnlyList<PricingItemResponse>> ReorderAsync(ReorderPricingItemsRequest request)
    {
        if (request.Ids.Count == 0)
            throw new PricingValidationException("Няма подадени цени за пренареждане.");

        var requestedIds = request.Ids.Distinct().ToList();
        var items = await context.PricingItems.ToListAsync();
        var itemMap = items.ToDictionary(x => x.Id);
        var order = 1;

        foreach (var id in requestedIds)
        {
            if (itemMap.TryGetValue(id, out var item))
                item.DisplayOrder = order++;
        }

        foreach (var remaining in items
            .Where(x => !requestedIds.Contains(x.Id))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id))
        {
            remaining.DisplayOrder = order++;
        }

        await context.SaveChangesAsync();

        return await GetAllAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var item = await context.PricingItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return false;

        context.PricingItems.Remove(item);
        await context.SaveChangesAsync();
        await NormalizeDisplayOrderAsync();

        return true;
    }

    public static string? Validate(PricingItemRequest request)
    {
        var pricingMode = NormalizePricingMode(request.PricingMode);
        if (string.IsNullOrWhiteSpace(request.Title)) return "Заглавието е задължително.";
        if (string.IsNullOrWhiteSpace(request.Description)) return "Описанието е задължително.";
        if (pricingMode == "Fixed" && string.IsNullOrWhiteSpace(request.PriceText)) return "Цената е задължителна при фиксирана цена.";
        return null;
    }

    public static string NormalizePricingMode(string? value) =>
        string.Equals(value, "Negotiable", StringComparison.OrdinalIgnoreCase) ? "Negotiable" : "Fixed";

    private async Task NormalizeDisplayOrderAsync()
    {
        var items = await context.PricingItems
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
            items[i].DisplayOrder = i + 1;

        await context.SaveChangesAsync();
    }

    private static PricingItemResponse ToResponse(PricingItem item) => new(
        item.Id,
        item.Title,
        item.Description,
        item.PricingMode,
        item.PriceText,
        item.DisplayOrder,
        item.IsActive,
        item.CreatedAtUtc);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PricingValidationException(string message) : Exception(message);
