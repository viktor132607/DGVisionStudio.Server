using System.Net;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services;

public class CalendarReminderEmailService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CalendarReminderEmailService> _logger;

    public CalendarReminderEmailService(IServiceScopeFactory scopeFactory, ILogger<CalendarReminderEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task SendDueReminders(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var windowEnd = now.AddHours(24);

        var events = await context.CalendarEvents
            .Where(x =>
                x.RemindersEnabled &&
                x.EventType == "Photoshoot" &&
                x.StartAtUtc > now &&
                x.StartAtUtc <= windowEnd &&
                x.ClientEmail != null &&
                x.ClientEmail != "" &&
                (x.Reminder24hSentAtUtc == null || x.Reminder2hSentAtUtc == null))
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var calendarEvent in events)
        {
            var hoursUntilStart = (calendarEvent.StartAtUtc - now).TotalHours;

            if (hoursUntilStart <= 24 && calendarEvent.Reminder24hSentAtUtc == null)
            {
                await SendReminder(context, emailService, calendarEvent, "24h", cancellationToken);
                calendarEvent.Reminder24hSentAtUtc = DateTime.UtcNow;
            }

            if (hoursUntilStart <= 2 && calendarEvent.Reminder2hSentAtUtc == null)
            {
                await SendReminder(context, emailService, calendarEvent, "2h", cancellationToken);
                calendarEvent.Reminder2hSentAtUtc = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SendReminder(
        AppDbContext context,
        IEmailService emailService,
        CalendarEvent calendarEvent,
        string reminderType,
        CancellationToken cancellationToken)
    {
        var toEmail = calendarEvent.ClientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var localStart = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTime.SpecifyKind(calendarEvent.StartAtUtc, DateTimeKind.Utc),
            "Europe/Sofia");

        var subject = reminderType == "24h"
            ? "Напомняне за фотосесия утре"
            : "Напомняне за фотосесия след 2 часа";

        var safeClientName = WebUtility.HtmlEncode(calendarEvent.ClientName ?? "");
        var safeTitle = WebUtility.HtmlEncode(calendarEvent.Title);
        var safeLocation = WebUtility.HtmlEncode(calendarEvent.Location ?? "Търговски комплекс Ялта, Русе");
        var safePhone = WebUtility.HtmlEncode(calendarEvent.ClientPhone ?? "");
        var safeAssignedTo = WebUtility.HtmlEncode(calendarEvent.AssignedTo ?? "DG Vision Studio");
        var safeNotes = WebUtility.HtmlEncode(calendarEvent.Description ?? "").Replace("\n", "<br />");
        var formattedDate = localStart.ToString("dd.MM.yyyy HH:mm");

        var body = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#111;line-height:1.6;">
                <h2 style="margin:0 0 16px;">Напомняне за фотосесия</h2>
                <p>Здравейте{(string.IsNullOrWhiteSpace(safeClientName) ? "" : $", {safeClientName}")},</p>
                <p>Напомняме Ви за записания час за фотосесия:</p>
                <p><strong>Събитие:</strong> {safeTitle}</p>
                <p><strong>Дата и час:</strong> {formattedDate}</p>
                <p><strong>Локация:</strong> {safeLocation}</p>
                <p><strong>Екип:</strong> {safeAssignedTo}</p>
                {(string.IsNullOrWhiteSpace(safePhone) ? "" : $"<p><strong>Телефон:</strong> {safePhone}</p>")}
                {(string.IsNullOrWhiteSpace(safeNotes) ? "" : $"<p><strong>Бележки:</strong><br />{safeNotes}</p>")}
                <p style="margin-top:20px;">Поздрави,<br />DG Vision Studio</p>
            </div>
            """;

        try
        {
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
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
