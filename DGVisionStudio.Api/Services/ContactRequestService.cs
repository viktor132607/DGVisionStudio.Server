using System.Net;
using System.Text.RegularExpressions;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestService : IContactRequestService
{
    private static readonly Regex PhoneRegex = new(
        @"^\+?[0-9\s().-]{7,20}$",
        RegexOptions.Compiled);

    private readonly AppDbContext _context;
    private readonly IEmailService? _emailService;
    private readonly IConfiguration? _configuration;

    public ContactRequestService(
        AppDbContext context,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
    }

    public ContactRequestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> CreateAsync(CreateContactRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Phone))
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "Name, email and phone are required."
            });
        }

        var normalizedPhone = dto.Phone.Trim();
        var phoneDigitsCount = normalizedPhone.Count(char.IsDigit);
        if (!PhoneRegex.IsMatch(normalizedPhone) || phoneDigitsCount is < 7 or > 15)
            return ControllerServiceResult.BadRequest(new { message = "Invalid phone number." });

        var entity = new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim(),
            Phone = normalizedPhone,
            Subject = Normalize(dto.Subject),
            Message = string.IsNullOrWhiteSpace(dto.Message) ? "-" : dto.Message.Trim(),
            IsSeenByAdmin = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.ContactRequests.Add(entity);
        await _context.SaveChangesAsync();
        await SendOwnerNotificationAsync(entity);

        return ControllerServiceResult.Ok(new
        {
            message = "Contact request submitted successfully.",
            id = entity.Id
        });
    }

    public async Task<ControllerServiceResult> MarkAllSeenAsync()
    {
        var requests = await _context.ContactRequests
            .Where(x => !x.IsSeenByAdmin && !x.IsArchived)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var request in requests)
        {
            request.IsSeenByAdmin = true;
            request.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await _context.ContactRequests
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetAsync(Guid id)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> UpdateAsync(Guid id, UpdateContactRequestDto dto)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        item.Status = dto.Status;
        item.AdminComment = dto.AdminComment;
        item.IsArchived = dto.IsArchived;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> UpdateStatusAsync(Guid id, UpdateContactRequestDto dto)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        item.Status = dto.Status;
        item.IsArchived = dto.Status is ContactRequestStatus.Completed or ContactRequestStatus.Rejected;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> DeleteAsync(Guid id)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        _context.ContactRequests.Remove(item);
        await _context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    private async Task SendOwnerNotificationAsync(ContactRequest entity)
    {
        var ownerEmail = _configuration?["Resend:OwnerEmail"];
        if (_emailService is null || string.IsNullOrWhiteSpace(ownerEmail))
            return;

        var safeName = WebUtility.HtmlEncode(entity.Name);
        var safeEmail = WebUtility.HtmlEncode(entity.Email);
        var safePhone = WebUtility.HtmlEncode(entity.Phone ?? "-");
        var safeSubject = WebUtility.HtmlEncode(entity.Subject ?? "-");
        var safeMessage = WebUtility.HtmlEncode(entity.Message).Replace("\n", "<br />");
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
            await _emailService.SendAsync(ownerEmail, mailSubject, body);
            log.IsSent = true;
            log.SentAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            log.IsSent = false;
            log.ErrorMessage = ex.Message;
        }

        _context.EmailLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
