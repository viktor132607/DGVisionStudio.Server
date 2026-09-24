using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class CalendarReminderProcessor(
    AppDbContext context,
    CalendarReminderDueQueryService dueQuery,
    CalendarReminderDeliveryService delivery,
    ILogger<CalendarReminderProcessor> logger)
{
    public async Task<int> ProcessDueAsync(
        CancellationToken cancellationToken = default)
    {
        var reminders = await dueQuery.GetDueAsync(
            DateTime.UtcNow,
            cancellationToken);

        if (reminders.Count == 0)
        {
            logger.LogDebug(
                "Calendar reminder check completed with no due reminders.");
            return 0;
        }

        logger.LogInformation(
            "Calendar reminder check found {ReminderCount} due reminder(s).",
            reminders.Count);

        var sentCount = 0;

        foreach (var reminder in reminders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sent = await delivery.TrySendAsync(
                reminder,
                cancellationToken);

            if (sent)
            {
                var sentAtUtc = DateTime.UtcNow;

                if (reminder.Kind == CalendarReminderKind.TwoHours)
                    reminder.CalendarEvent.Reminder2hSentAtUtc = sentAtUtc;
                else
                    reminder.CalendarEvent.Reminder24hSentAtUtc = sentAtUtc;

                sentCount++;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }
}
