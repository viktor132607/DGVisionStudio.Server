using System.Net;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactRequestsController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;

	public ContactRequestsController(AppDbContext context, IEmailService emailService, IConfiguration configuration)
	{
		_context = context;
		_emailService = emailService;
		_configuration = configuration;
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateContactRequestDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Message))
			return BadRequest(new { message = "Name, email and message are required." });

		var entity = new ContactRequest
		{
			Id = Guid.NewGuid(),
			Name = dto.Name.Trim(),
			Email = dto.Email.Trim(),
			Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
			Subject = string.IsNullOrWhiteSpace(dto.Subject) ? null : dto.Subject.Trim(),
			Message = dto.Message.Trim(),
			CreatedAtUtc = DateTime.UtcNow
		};

		_context.ContactRequests.Add(entity);
		await _context.SaveChangesAsync();

		var ownerEmail = _configuration["Resend:OwnerEmail"];
		if (!string.IsNullOrWhiteSpace(ownerEmail))
		{
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

			try
			{
				await _emailService.SendAsync(ownerEmail, mailSubject, body);
				_context.EmailLogs.Add(new EmailLog
				{
					Id = Guid.NewGuid(),
					ContactRequestId = entity.Id,
					ToEmail = ownerEmail,
					Subject = mailSubject,
					Body = body,
					IsSent = true,
					SentAtUtc = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				_context.EmailLogs.Add(new EmailLog
				{
					Id = Guid.NewGuid(),
					ContactRequestId = entity.Id,
					ToEmail = ownerEmail,
					Subject = mailSubject,
					Body = body,
					IsSent = false,
					ErrorMessage = ex.Message
				});
			}

			await _context.SaveChangesAsync();
		}

		return Ok(new { message = "Contact request submitted successfully.", id = entity.Id });
	}
}