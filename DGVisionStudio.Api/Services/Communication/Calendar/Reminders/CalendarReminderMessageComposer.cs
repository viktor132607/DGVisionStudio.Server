using System.Net;
using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Infrastructure.Services;

public sealed record CalendarReminderMessage(
    string ToEmail,
    string Subject,
    string Body);

public sealed class CalendarReminderMessageComposer(
    ILogger<CalendarReminderMessageComposer> logger)
{
    private const string BrandName = "DG Vision Studio";
    private const string WebsiteUrl = "https://dgvisionstudio.com";
    private const string LogoUrl = WebsiteUrl + "/images/relogo/black.webp";

    public CalendarReminderMessage? Compose(
        CalendarEvent calendarEvent,
        CalendarReminderKind kind)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var toEmail = calendarEvent.ClientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(toEmail))
            return null;

        var subject = kind == CalendarReminderKind.TwentyFourHours
            ? "Напомняне за фотосесия утре"
            : "Напомняне за фотосесия след 2 часа";

        var localStart = ConvertToSofiaTime(calendarEvent.StartAtUtc);
        var safeClientName = WebUtility.HtmlEncode(calendarEvent.ClientName ?? string.Empty);
        var safeTitle = WebUtility.HtmlEncode(calendarEvent.Title);
        var safeLocation = WebUtility.HtmlEncode(
            calendarEvent.Location ?? "Търговски комплекс Ялта, Русе");
        var safePhone = WebUtility.HtmlEncode(calendarEvent.ClientPhone ?? string.Empty);
        var safeAssignedTo = WebUtility.HtmlEncode(calendarEvent.AssignedTo ?? BrandName);
        var safeNotes = WebUtility.HtmlEncode(calendarEvent.Description ?? string.Empty)
            .Replace("\n", "<br />");
        var formattedDate = localStart.ToString("dd.MM.yyyy HH:mm");

        var body = $"""
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
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin-top:24px;">
                    <tr>
                        <td>
                            <p style="margin:0 0 12px;">Поздрави,<br /><strong>{BrandName}</strong></p>
                            <a href="{WebsiteUrl}" target="_blank" style="display:inline-block;text-decoration:none;">
                                <img src="{LogoUrl}" width="180" alt="{BrandName}" style="display:block;width:180px;max-width:100%;height:auto;border:0;outline:none;text-decoration:none;" />
                            </a>
                        </td>
                    </tr>
                </table>
            </div>
            """;

        return new CalendarReminderMessage(toEmail, subject, body);
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
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        logger.LogWarning(
            "Sofia time zone was not available. Calendar reminder for {StartAtUtc} will display UTC time.",
            utcStart);

        return utcStart;
    }
}
