using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminCalendarContactRequestQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetForImportAsync()
    {
        var items = await context.ContactRequests
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.Subject,
                x.Message,
                x.CreatedAtUtc,
                x.Status
            })
            .ToListAsync();

        return ControllerServiceResult.Ok(items);
    }

    public async Task<ControllerServiceResult> GetForImportAsync(Guid id)
    {
        var item = await context.ContactRequests
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.Subject,
                x.Message,
                x.CreatedAtUtc,
                x.Status
            })
            .FirstOrDefaultAsync();

        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }
}
