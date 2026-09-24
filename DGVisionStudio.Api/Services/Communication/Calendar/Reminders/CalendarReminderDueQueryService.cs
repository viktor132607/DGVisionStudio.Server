using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services;

public enum CalendarReminderKind
{
    TwoHours,
    TwentyFourHours
}

public sealed record DueCalendarReminder(
    CalendarEvent CalendarEvent,
    CalendarReminderKind Kind);

public sealed class CalendarReminderDueQueryService(AppDbContext context)
{
    private static readonly TimeSpan TwoHourReminderWindow = TimeSpan.FromHours(2);
    private static readonly TimeSpan TwentyFourHourReminderWindow = TimeSpan.FromHours(24);

    public async Task<List<DueCalendarReminder>> GetDueAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var twoHourWindowEnd = nowUtc.Add(TwoHourReminderWindow);
        var twentyFourHourWindowEnd = nowUtc.Add(TwentyFourHourReminderWindow);

        var events = await context.CalendarEvents
            .Where(calendarEvent =>
                calendarEvent.RemindersEnabled &&
                calendarEvent.EventType == "Photoshoot" &&
                calendarEvent.StartAtUtc > nowUtc &&
                calendarEvent.ClientEmail != null &&
                calendarEvent.ClientEmail != "" &&
                (
                    (calendarEvent.Reminder2hSentAtUtc == null &&
                     calendarEvent.StartAtUtc <= twoHourWindowEnd) ||
                    (calendarEvent.Reminder24hSentAtUtc == null &&
                     calendarEvent.StartAtUtc > twoHourWindowEnd &&
                     calendarEvent.StartAtUtc <= twentyFourHourWindowEnd)
                ))
            .OrderBy(calendarEvent => calendarEvent.StartAtUtc)
            .ToListAsync(cancellationToken);

        return events
            .Select(calendarEvent => new DueCalendarReminder(
                calendarEvent,
                calendarEvent.StartAtUtc <= twoHourWindowEnd
                    ? CalendarReminderKind.TwoHours
                    : CalendarReminderKind.TwentyFourHours))
            .ToList();
    }
}
