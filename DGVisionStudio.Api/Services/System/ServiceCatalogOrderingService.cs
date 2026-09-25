using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ServiceCatalogOrderingService(AppDbContext context)
{
    public async Task<int> GetNextDisplayOrderAsync()
    {
        var maxOrder = await context.Services.AnyAsync()
            ? await context.Services.MaxAsync(service => service.DisplayOrder)
            : 0;

        return maxOrder + 1;
    }

    public async Task<List<Service>> ReorderAsync(IReadOnlyCollection<int> ids)
    {
        var services = await context.Services.ToListAsync();
        var byId = services.ToDictionary(service => service.Id);
        var order = 1;

        foreach (var id in ids.Distinct())
        {
            if (byId.TryGetValue(id, out var item))
                item.DisplayOrder = order++;
        }

        foreach (var item in services
            .Where(service => !ids.Contains(service.Id))
            .OrderBy(service => service.DisplayOrder)
            .ThenBy(service => service.Id))
        {
            item.DisplayOrder = order++;
        }

        await context.SaveChangesAsync();

        return await context.Services
            .OrderBy(service => service.DisplayOrder)
            .ThenBy(service => service.Id)
            .ToListAsync();
    }

    public async Task NormalizeDisplayOrderAsync()
    {
        var items = await context.Services
            .OrderBy(service => service.DisplayOrder)
            .ThenBy(service => service.Id)
            .ToListAsync();

        for (var index = 0; index < items.Count; index++)
            items[index].DisplayOrder = index + 1;

        await context.SaveChangesAsync();
    }
}
