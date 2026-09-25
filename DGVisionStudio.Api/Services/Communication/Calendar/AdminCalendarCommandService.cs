using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Api.Services;

public sealed class AdminCalendarCommandService(
    AppDbContext context,
    AdminCalendarEventInputService input)
{
    public async Task<ControllerServiceResult> CreateAsync(CalendarEventDto dto)
    {
        var validation = input.Validate(dto);
        if (validation is not null)
            return ControllerServiceResult.BadRequest(new { message = validation });

        var item = await input.CreateAsync(dto);
        context.CalendarEvents.Add(item);
        await context.SaveChangesAsync();

        return new ControllerServiceResult(StatusCodes.Status201Created, item);
    }

    public async Task<ControllerServiceResult> UpdateAsync(int id, CalendarEventDto dto)
    {
        var item = await context.CalendarEvents.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        var validation = input.Validate(dto);
        if (validation is not null)
            return ControllerServiceResult.BadRequest(new { message = validation });

        await input.UpdateAsync(item, dto);
        await context.SaveChangesAsync();

        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        var item = await context.CalendarEvents.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        context.CalendarEvents.Remove(item);
        await context.SaveChangesAsync();

        return ControllerServiceResult.NoContent();
    }
}
