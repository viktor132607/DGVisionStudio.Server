using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminCalendarEventInputService(AppDbContext context)
{
    public string? Validate(CalendarEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return "Заглавието е задължително.";

        if (dto.EndAtUtc <= dto.StartAtUtc)
            return "Краят трябва да е след началото.";

        return null;
    }

    public async Task<CalendarEvent> CreateAsync(CalendarEventDto dto)
    {
        var item = new CalendarEvent
        {
            CreatedAtUtc = DateTime.UtcNow
        };

        await ApplyValuesAsync(item, dto);
        return item;
    }

    public async Task UpdateAsync(CalendarEvent item, CalendarEventDto dto)
    {
        var previousStart = item.StartAtUtc;
        var previousEmail = item.ClientEmail;

        await ApplyValuesAsync(item, dto);
        item.UpdatedAtUtc = DateTime.UtcNow;

        if (previousStart != item.StartAtUtc ||
            !string.Equals(previousEmail, item.ClientEmail, StringComparison.OrdinalIgnoreCase))
        {
            item.Reminder24hSentAtUtc = null;
            item.Reminder2hSentAtUtc = null;
        }
    }

    private async Task ApplyValuesAsync(CalendarEvent item, CalendarEventDto dto)
    {
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
    }

    private async Task<Guid?> NormalizeContactRequestIdAsync(Guid? value)
    {
        if (!value.HasValue || value.Value == Guid.Empty)
            return null;

        return await context.ContactRequests.AnyAsync(x => x.Id == value.Value)
            ? value.Value
            : null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
