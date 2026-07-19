using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class SiteSettingsService(AppDbContext context) : ISiteSettingsService
{
    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await context.SiteSettings
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                x.Key,
                x.Value,
                x.Description,
                x.UpdatedAtUtc
            })
            .ToListAsync());
}
