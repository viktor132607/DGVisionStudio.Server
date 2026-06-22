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

        var item = new CalendarEvent
        {
            Title = dto.Title.Trim(),
            EventType = Normalize(dto.EventType) ?? "Photoshoot",
            AssignedTo = Normalize(dto.AssignedTo),
            ClientName = Normalize(dto.ClientName),
            ClientPhone = Normalize(dto.ClientPhone),
            Location = Normalize(dto.Location),
            Description = Normalize(dto.Description),
            Color = Normalize(dto.Color),
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

        item.Title = dto.Title.Trim();
        item.EventType = Normalize(dto.EventType) ?? "Photoshoot";
        item.AssignedTo = Normalize(dto.AssignedTo);
        item.ClientName = Normalize(dto.ClientName);
        item.ClientPhone = Normalize(dto.ClientPhone);
        item.Location = Normalize(dto.Location);
        item.Description = Normalize(dto.Description);
        item.Color = Normalize(dto.Color);
        item.StartAtUtc = ToUtc(dto.StartAtUtc);
        item.EndAtUtc = ToUtc(dto.EndAtUtc);
        item.UpdatedAtUtc = DateTime.UtcNow;

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
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
}
