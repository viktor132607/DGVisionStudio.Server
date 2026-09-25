using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminCalendarQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await context.CalendarEvents
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetAsync(int id)
    {
        var item = await context.CalendarEvents.FindAsync(id);
        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }
}
