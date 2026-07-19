using System.Net;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services;

public class CalendarReminderEmailService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TwoHourReminderWindow = TimeSpan.FromHours(2);
    private static readonly TimeSpan TwentyFourHourReminderWindow = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CalendarReminderEmailService> _logger;

    public CalendarReminderEmailService(
        IServiceScopeFactory scopeFactory,
        ILogger<CalendarReminderEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Calendar reminder worker started. Check interval: {CheckIntervalMinutes} minutes.",
            CheckInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueReminders(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Calendar reminder check failed.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task SendDueReminders(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var twoHourWindowEnd = now.Add(TwoHourReminderWindow);
        var twentyFourHourWindowEnd = now.Add(TwentyFourHourReminderWindow);

        var events = await context.CalendarEvents
            .Where(calendarEvent =>
                calendarEvent.RemindersEnabled &&
                calendarEvent.EventType == "Photoshoot" &&
                calendarEvent.StartAtUtc > now &&
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

        if (events.Count == 0)
        {
            _logger.LogDebug("Calendar reminder check completed with no due reminders.");
            return;
        }

        _logger.LogInformation(
            "Calendar reminder check found {ReminderCount} due reminder(s).",
            events.Count);

        foreach (var calendarEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isTwoHourReminder = calendarEvent.StartAtUtc <= twoHourWindowEnd;
            var reminderType = isTwoHourReminder ? "2h" : "24h";
            var sent = await TrySendReminder(
                context,
                emailService,
                calendarEvent,
                reminderType,
                cancellationToken);

            if (sent)
            {
                var sentAtUtc = DateTime.UtcNow;

                if (isTwoHourReminder)
                {
                    calendarEvent.Reminder2hSentAtUtc = sentAtUtc;
                }
                else
                {
                    calendarEvent.Reminder24hSentAtUtc = sentAtUtc;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> TrySendReminder(
        AppDbContext context,
        IEmailService emailService,
        CalendarEvent calendarEvent,
        string reminderType,
        CancellationToken cancellationToken)
    {
        var toEmail = calendarEvent.ClientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return false;
        }

        var subject = reminderType == "24h"
            ? "Напомняне за фотосесия утре"
            : "Напомняне за фотосесия след 2 часа";

        var body = string.Empty;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localStart = ConvertToSofiaTime(calendarEvent.StartAtUtc);
            var safeClientName = WebUtility.HtmlEncode(calendarEvent.ClientName ?? string.Empty);
            var safeTitle = WebUtility.HtmlEncode(calendarEvent.Title);
            var safeLocation = WebUtility.HtmlEncode(calendarEvent.Location ?? "Търговски комплекс Ялта, Русе");
            var safePhone = WebUtility.HtmlEncode(calendarEvent.ClientPhone ?? string.Empty);
            var safeAssignedTo = WebUtility.HtmlEncode(calendarEvent.AssignedTo ?? "DG Vision Studio");
            var safeNotes = WebUtility.HtmlEncode(calendarEvent.Description ?? string.Empty).Replace("\n", "<br />");
            var formattedDate = localStart.ToString("dd.MM.yyyy HH:mm");

            body = $"""
                <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#111;line-height:1.6;">
                    <h2 style="margin:0 0 16px;">Напомняне за фотосесия</h2>
                    <p>Здравейте{(string.IsNullOrWhiteSpace(safeClientName) ? string.Empty : $", {safeClientName}")},</p>
                    <p>Напомняме Ви за записания час за фотосесия:</p>
                    <p><strong>Събитие:</strong> {safeTitle}</p>
                    <p><strong>Дата и час:</strong> {formattedDate}</p>
                    <p><strong>Локация:</strong> {safeLocation}</p>
                    <p><strong>Екип:</strong> {safeAssignedTo}</p>
                    {(string.IsNullOrWhiteSpace(safePhone) ? string.Empty : $"<p><strong>Телефон:</strong> {safePhone}</p>")}
                    {(string.IsNullOrWhiteSpace(safeNotes) ? string.Empty : $"<p><strong>Бележки:</strong><br />{safeNotes}</p>")}
                    <p style="margin-top:20px;">Поздрави,<br />DG Vision Studio</p>
                </div>
                """;

            await emailService.SendAsync(toEmail, subject, body);

            context.EmailLogs.Add(new EmailLog
            {
                Id = Guid.NewGuid(),
                ContactRequestId = calendarEvent.ContactRequestId,
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                IsSent = true,
                SentAtUtc = DateTime.UtcNow
            });

            _logger.LogInformation(
                "Calendar {ReminderType} reminder sent for event {CalendarEventId} to {RecipientEmail}.",
                reminderType,
                calendarEvent.Id,
                toEmail);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.EmailLogs.Add(new EmailLog
            {
                Id = Guid.NewGuid(),
                ContactRequestId = calendarEvent.ContactRequestId,
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                IsSent = false,
                ErrorMessage = ex.Message
            });

            _logger.LogError(
                ex,
                "Calendar {ReminderType} reminder failed for event {CalendarEventId} and will be retried.",
                reminderType,
                calendarEvent.Id);

            return false;
        }
    }

    private DateTime ConvertToSofiaTime(DateTime startAtUtc)
    {
        var utcStart = DateTime.SpecifyKind(startAtUtc, DateTimeKind.Utc);

        foreach (var timeZoneId in new[] { "Europe/Sofia", "FLE Standard Time" })
        {
            try
            {
                return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utcStart, timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next platform-specific time-zone identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the next platform-specific time-zone identifier.
            }
        }

        _logger.LogWarning(
            "Sofia time zone was not available. Calendar reminder for {StartAtUtc} will display UTC time.",
            utcStart);

        return utcStart;
    }
}
