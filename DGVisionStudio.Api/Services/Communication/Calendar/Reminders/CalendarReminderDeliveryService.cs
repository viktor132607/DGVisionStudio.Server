using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class CalendarReminderDeliveryService(
    AppDbContext context,
    IEmailService emailService,
    CalendarReminderMessageComposer composer,
    ILogger<CalendarReminderDeliveryService> logger)
{
    public async Task<bool> TrySendAsync(
        DueCalendarReminder reminder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        var message = composer.Compose(reminder.CalendarEvent, reminder.Kind);
        if (message is null)
            return false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await emailService.SendAsync(
                message.ToEmail,
                message.Subject,
                message.Body);

            context.EmailLogs.Add(CreateLog(
                reminder.CalendarEvent,
                message,
                isSent: true,
                errorMessage: null));

            logger.LogInformation(
                "Calendar {ReminderType} reminder sent for event {CalendarEventId} to {RecipientEmail}.",
                ToLogLabel(reminder.Kind),
                reminder.CalendarEvent.Id,
                message.ToEmail);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.EmailLogs.Add(CreateLog(
                reminder.CalendarEvent,
                message,
                isSent: false,
                errorMessage: ex.Message));

            logger.LogError(
                ex,
                "Calendar {ReminderType} reminder failed for event {CalendarEventId} and will be retried.",
                ToLogLabel(reminder.Kind),
                reminder.CalendarEvent.Id);

            return false;
        }
    }

    private static EmailLog CreateLog(
        CalendarEvent calendarEvent,
        CalendarReminderMessage message,
        bool isSent,
        string? errorMessage) =>
        new()
        {
            Id = Guid.NewGuid(),
            ContactRequestId = calendarEvent.ContactRequestId,
            ToEmail = message.ToEmail,
            Subject = message.Subject,
            Body = message.Body,
            IsSent = isSent,
            ErrorMessage = errorMessage,
            SentAtUtc = isSent ? DateTime.UtcNow : null
        };

    private static string ToLogLabel(CalendarReminderKind kind) =>
        kind == CalendarReminderKind.TwoHours ? "2h" : "24h";
}
