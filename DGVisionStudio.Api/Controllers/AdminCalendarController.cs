using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/calendar")]
public class AdminCalendarController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminCalendarController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.CalendarEvents.OrderBy(x => x.StartAtUtc).ToListAsync();
        return Ok(items);
    }

    [HttpGet("contact-requests")]
    public async Task<IActionResult> GetContactRequestsForImport()
    {
        var items = await _context.ContactRequests
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.Subject,
                x.Message,
                x.CreatedAtUtc,
                x.Status
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("contact-requests/{id:guid}")]
    public async Task<IActionResult> GetContactRequestForImport(Guid id)
    {
        var item = await _context.ContactRequests
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.Subject,
                x.Message,
                x.CreatedAtUtc,
                x.Status
            })
            .FirstOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _context.CalendarEvents.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CalendarEventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "Заглавието е задължително." });
        if (dto.EndAtUtc <= dto.StartAtUtc) return BadRequest(new { message = "Краят трябва да е след началото." });

        var contactRequestId = await NormalizeContactRequestId(dto.ContactRequestId);

        var item = new CalendarEvent
        {
            Title = dto.Title.Trim(),
            EventType = Normalize(dto.EventType) ?? "Photoshoot",
            AssignedTo = Normalize(dto.AssignedTo),
            ClientName = Normalize(dto.ClientName),
            ClientPhone = Normalize(dto.ClientPhone),
            ClientEmail = Normalize(dto.ClientEmail),
            ContactRequestId = contactRequestId,
            Location = Normalize(dto.Location),
            Description = Normalize(dto.Description),
            Color = Normalize(dto.Color),
            RemindersEnabled = dto.RemindersEnabled,
            StartAtUtc = ToUtc(dto.StartAtUtc),
            EndAtUtc = ToUtc(dto.EndAtUtc),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.CalendarEvents.Add(item);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CalendarEventDto dto)
    {
        var item = await _context.CalendarEvents.FindAsync(id);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "Заглавието е задължително." });
        if (dto.EndAtUtc <= dto.StartAtUtc) return BadRequest(new { message = "Краят трябва да е след началото." });

        var previousStart = item.StartAtUtc;
        var previousEmail = item.ClientEmail;
        var contactRequestId = await NormalizeContactRequestId(dto.ContactRequestId);

        item.Title = dto.Title.Trim();
        item.EventType = Normalize(dto.EventType) ?? "Photoshoot";
        item.AssignedTo = Normalize(dto.AssignedTo);
        item.ClientName = Normalize(dto.ClientName);
        item.ClientPhone = Normalize(dto.ClientPhone);
        item.ClientEmail = Normalize(dto.ClientEmail);
        item.ContactRequestId = contactRequestId;
        item.Location = Normalize(dto.Location);
        item.Description = Normalize(dto.Description);
        item.Color = Normalize(dto.Color);
        item.RemindersEnabled = dto.RemindersEnabled;
        item.StartAtUtc = ToUtc(dto.StartAtUtc);
        item.EndAtUtc = ToUtc(dto.EndAtUtc);
        item.UpdatedAtUtc = DateTime.UtcNow;

        if (previousStart != item.StartAtUtc || !string.Equals(previousEmail, item.ClientEmail, StringComparison.OrdinalIgnoreCase))
        {
            item.Reminder24hSentAtUtc = null;
            item.Reminder2hSentAtUtc = null;
        }

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.CalendarEvents.FindAsync(id);
        if (item is null) return NotFound();
        _context.CalendarEvents.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Guid?> NormalizeContactRequestId(Guid? value)
    {
        if (!value.HasValue || value.Value == Guid.Empty) return null;
        var exists = await _context.ContactRequests.AnyAsync(x => x.Id == value.Value);
        return exists ? value.Value : null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public class CalendarEventDto
{
    public string Title { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public string? AssignedTo { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientEmail { get; set; }
    public Guid? ContactRequestId { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool RemindersEnabled { get; set; } = true;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
}
