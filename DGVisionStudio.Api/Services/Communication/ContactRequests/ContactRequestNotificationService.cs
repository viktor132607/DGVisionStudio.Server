using System.Net;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestNotificationService
{
    private readonly AppDbContext context;
    private readonly IEmailService? emailService;
    private readonly string? ownerEmail;

    public ContactRequestNotificationService(
        AppDbContext context,
        IEmailService emailService,
        IConfiguration configuration)
    {
        this.context = context;
        this.emailService = emailService;
        ownerEmail = configuration["Resend:OwnerEmail"];
    }

    public ContactRequestNotificationService(AppDbContext context)
    {
        this.context = context;
    }

    public async Task SendOwnerNotificationAsync(ContactRequest entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (emailService is null ||
            string.IsNullOrWhiteSpace(ownerEmail))
        {
            return;
        }

        var safeName = WebUtility.HtmlEncode(entity.Name);
        var safeEmail = WebUtility.HtmlEncode(entity.Email);
        var safePhone = WebUtility.HtmlEncode(entity.Phone ?? "-");
        var safeSubject = WebUtility.HtmlEncode(entity.Subject ?? "-");
        var safeMessage = WebUtility
            .HtmlEncode(entity.Message)
            .Replace("\n", "<br />");

        var mailSubject = $"New contact request - {entity.Name}";
        var body = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#111;">
                <h2 style="margin:0 0 16px;">New contact request</h2>
                <p><strong>Name:</strong> {safeName}</p>
                <p><strong>Email:</strong> {safeEmail}</p>
                <p><strong>Phone:</strong> {safePhone}</p>
                <p><strong>Subject:</strong> {safeSubject}</p>
                <p><strong>Message:</strong></p>
                <p>{safeMessage}</p>
            </div>
            """;

        var log = new EmailLog
        {
            Id = Guid.NewGuid(),
            ContactRequestId = entity.Id,
            ToEmail = ownerEmail,
            Subject = mailSubject,
            Body = body
        };

        try
        {
            await emailService.SendAsync(
                ownerEmail,
                mailSubject,
                body);

            log.IsSent = true;
            log.SentAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            log.IsSent = false;
            log.ErrorMessage = ex.Message;
        }

        context.EmailLogs.Add(log);
        await context.SaveChangesAsync();
    }
}
