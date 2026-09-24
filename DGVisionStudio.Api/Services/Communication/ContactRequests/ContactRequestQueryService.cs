using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestQueryService(AppDbContext context)
{
    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(
            await context.ContactRequests
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync());

    public async Task<ControllerServiceResult> GetAsync(Guid id)
    {
        var item = await context.ContactRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }
}
