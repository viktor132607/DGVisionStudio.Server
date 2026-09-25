using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ServiceCatalogQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetActiveAsync() =>
        ControllerServiceResult.Ok(await context.Services
            .Where(service => service.IsActive)
            .OrderBy(service => service.DisplayOrder)
            .ThenBy(service => service.Id)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetPublicByIdAsync(int id)
    {
        var item = await context.Services.FindAsync(id);

        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await context.Services
            .OrderBy(service => service.DisplayOrder)
            .ThenBy(service => service.Id)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetAsync(int id)
    {
        var item = await context.Services.FindAsync(id);

        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }
}
