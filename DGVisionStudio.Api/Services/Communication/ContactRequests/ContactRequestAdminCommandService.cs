using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestAdminCommandService(
    AppDbContext context)
{
    public async Task<ControllerServiceResult> MarkAllSeenAsync()
    {
        var requests = await context.ContactRequests
            .Where(x => !x.IsSeenByAdmin && !x.IsArchived)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var request in requests)
        {
            request.IsSeenByAdmin = true;
            request.UpdatedAtUtc = now;
        }

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> UpdateAsync(
        Guid id,
        UpdateContactRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var item = await context.ContactRequests.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        item.Status = dto.Status;
        item.AdminComment = dto.AdminComment;
        item.IsArchived = dto.IsArchived;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> UpdateStatusAsync(
        Guid id,
        UpdateContactRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var item = await context.ContactRequests.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        item.Status = dto.Status;
        item.IsArchived =
            dto.Status is ContactRequestStatus.Completed
                or ContactRequestStatus.Rejected;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> DeleteAsync(Guid id)
    {
        var item = await context.ContactRequests.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        context.ContactRequests.Remove(item);
        await context.SaveChangesAsync();

        return ControllerServiceResult.NoContent();
    }
}
