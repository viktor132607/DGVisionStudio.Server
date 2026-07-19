using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminCalendarService : IAdminCalendarService
{
    private readonly AppDbContext _context;

    public AdminCalendarService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await _context.CalendarEvents
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetContactRequestsForImportAsync()
    {
        var items = await _context.ContactRequests
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

    public async Task<ControllerServiceResult> GetContactRequestForImportAsync(Guid id)
    {
        var item = await _context.ContactRequests
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

    public async Task<ControllerServiceResult> GetAsync(int id)
    {
        var item = await _context.CalendarEvents.FindAsync(id);
        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> CreateAsync(CalendarEventDto dto)
    {
        var validation = Validate(dto);
        if (validation is not null)
            return ControllerServiceResult.BadRequest(new { message = validation });

        var item = new CalendarEvent
        {
            Title = dto.Title.Trim(),
            EventType = Normalize(dto.EventType) ?? "Photoshoot",
            AssignedTo = Normalize(dto.AssignedTo),
            ClientName = Normalize(dto.ClientName),
            ClientPhone = Normalize(dto.ClientPhone),
            ClientEmail = Normalize(dto.ClientEmail),
            ContactRequestId = await NormalizeContactRequestIdAsync(dto.ContactRequestId),
            Location = Normalize(dto.Location),
            Description = Normalize(dto.Description),
            Color = Normalize(dto.Color),
            RemindersEnabled = dto.RemindersEnabled,
            StartAtUtc = ToUtc(dto.StartAtUtc),
            EndAtUtc = ToUtc(dto.EndAtUtc),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.CalendarEvents.Add(item);
        await _context.SaveChangesAsync();
        return new ControllerServiceResult(StatusCodes.Status201Created, item);
    }

    public async Task<ControllerServiceResult> UpdateAsync(int id, CalendarEventDto dto)
    {
        var item = await _context.CalendarEvents.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        var validation = Validate(dto);
        if (validation is not null)
            return ControllerServiceResult.BadRequest(new { message = validation });

        var previousStart = item.StartAtUtc;
        var previousEmail = item.ClientEmail;

        item.Title = dto.Title.Trim();
        item.EventType = Normalize(dto.EventType) ?? "Photoshoot";
        item.AssignedTo = Normalize(dto.AssignedTo);
        item.ClientName = Normalize(dto.ClientName);
        item.ClientPhone = Normalize(dto.ClientPhone);
        item.ClientEmail = Normalize(dto.ClientEmail);
        item.ContactRequestId = await NormalizeContactRequestIdAsync(dto.ContactRequestId);
        item.Location = Normalize(dto.Location);
        item.Description = Normalize(dto.Description);
        item.Color = Normalize(dto.Color);
        item.RemindersEnabled = dto.RemindersEnabled;
        item.StartAtUtc = ToUtc(dto.StartAtUtc);
        item.EndAtUtc = ToUtc(dto.EndAtUtc);
        item.UpdatedAtUtc = DateTime.UtcNow;

        if (previousStart != item.StartAtUtc ||
            !string.Equals(previousEmail, item.ClientEmail, StringComparison.OrdinalIgnoreCase))
        {
            item.Reminder24hSentAtUtc = null;
            item.Reminder2hSentAtUtc = null;
        }

        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        var item = await _context.CalendarEvents.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        _context.CalendarEvents.Remove(item);
        await _context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    private async Task<Guid?> NormalizeContactRequestIdAsync(Guid? value)
    {
        if (!value.HasValue || value.Value == Guid.Empty)
            return null;

        return await _context.ContactRequests.AnyAsync(x => x.Id == value.Value)
            ? value.Value
            : null;
    }

    private static string? Validate(CalendarEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return "Заглавието е задължително.";
        if (dto.EndAtUtc <= dto.StartAtUtc)
            return "Краят трябва да е след началото.";
        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
