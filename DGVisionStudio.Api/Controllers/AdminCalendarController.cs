using System.Text.Json;
using System.Text.RegularExpressions;
using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/calendar")]
public class AdminCalendarController : ControllerBase
{
    private const string EventTypesSettingKey = "CalendarEventTypes";
    private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly CalendarEventTypeDto[] DefaultEventTypes =
    [
        new() { Code = "Photoshoot", Name = "Фотосесия", Color = "#2563eb", IsSystem = true },
        new() { Code = "Print", Name = "Принт на снимки", Color = "#f97316", IsSystem = true }
    ];

    private readonly IAdminCalendarService _service;
    private readonly AppDbContext? _context;

    [ActivatorUtilitiesConstructor]
    public AdminCalendarController(IAdminCalendarService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    public AdminCalendarController(IAdminCalendarService service)
    {
        _service = service;
    }

    public AdminCalendarController(AppDbContext context)
        : this(new AdminCalendarService(context), context)
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetAllAsync());

    [HttpGet("contact-requests")]
    public async Task<IActionResult> GetContactRequestsForImport() =>
        this.ToActionResult(await _service.GetContactRequestsForImportAsync());

    [HttpGet("contact-requests/{id:guid}")]
    public async Task<IActionResult> GetContactRequestForImport(Guid id) =>
        this.ToActionResult(await _service.GetContactRequestForImportAsync(id));

    [HttpGet("event-types")]
    public async Task<IActionResult> GetEventTypes()
    {
        var eventTypes = await LoadEventTypesAsync();
        return Ok(eventTypes);
    }

    [HttpPost("event-types")]
    public async Task<IActionResult> CreateEventType([FromBody] CalendarEventTypeRequest request)
    {
        var validationError = ValidateEventTypeRequest(request);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var eventTypes = await LoadEventTypesAsync();
        var code = request.Name.Trim();

        if (eventTypes.Any(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Вече съществува тип с това име." });

        var created = new CalendarEventTypeDto
        {
            Code = code,
            Name = code,
            Color = request.Color.Trim().ToLowerInvariant(),
            IsSystem = false
        };

        eventTypes.Add(created);
        await SaveEventTypesAsync(eventTypes);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("event-types/{code}")]
    public async Task<IActionResult> UpdateEventType(string code, [FromBody] CalendarEventTypeRequest request)
    {
        var validationError = ValidateEventTypeRequest(request);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var context = RequireContext();
        var eventTypes = await LoadEventTypesAsync();
        var index = eventTypes.FindIndex(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return NotFound();

        var current = eventTypes[index];
        var nextCode = current.IsSystem ? current.Code : request.Name.Trim();

        if (eventTypes
            .Where((_, itemIndex) => itemIndex != index)
            .Any(item => item.Code.Equals(nextCode, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Вече съществува тип с това име." });
        }

        var nextColor = request.Color.Trim().ToLowerInvariant();
        eventTypes[index] = new CalendarEventTypeDto
        {
            Code = nextCode,
            Name = current.IsSystem ? current.Name : nextCode,
            Color = nextColor,
            IsSystem = current.IsSystem
        };

        var matchingEvents = await context.CalendarEvents
            .Where(x => x.EventType == current.Code)
            .ToListAsync();

        foreach (var calendarEvent in matchingEvents)
        {
            calendarEvent.EventType = nextCode;
            calendarEvent.Color = nextColor;
            calendarEvent.UpdatedAtUtc = DateTime.UtcNow;
        }

        await SaveEventTypesAsync(eventTypes);
        return Ok(eventTypes[index]);
    }

    [HttpDelete("event-types/{code}")]
    public async Task<IActionResult> DeleteEventType(string code)
    {
        var context = RequireContext();
        var eventTypes = await LoadEventTypesAsync();
        var eventType = eventTypes.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        if (eventType == null)
            return NotFound();

        if (eventType.IsSystem)
            return BadRequest(new { message = "Основните типове не могат да бъдат изтрити." });

        if (await context.CalendarEvents.AnyAsync(x => x.EventType == eventType.Code))
        {
            return BadRequest(new
            {
                message = "Типът се използва от събития в календара. Промени типа на тези събития преди изтриване."
            });
        }

        eventTypes.Remove(eventType);
        await SaveEventTypesAsync(eventTypes);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        this.ToActionResult(await _service.GetAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CalendarEventDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return result.StatusCode == StatusCodes.Status201Created && result.Value is CalendarEvent item
            ? CreatedAtAction(nameof(Get), new { id = item.Id }, item)
            : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CalendarEventDto dto) =>
        this.ToActionResult(await _service.UpdateAsync(id, dto));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        this.ToActionResult(await _service.DeleteAsync(id));

    private AppDbContext RequireContext() =>
        _context ?? throw new InvalidOperationException("Calendar event type storage is unavailable.");

    private async Task<List<CalendarEventTypeDto>> LoadEventTypesAsync()
    {
        var context = RequireContext();
        var setting = await context.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == EventTypesSettingKey);

        List<CalendarEventTypeDto> storedTypes;

        try
        {
            storedTypes = setting == null
                ? []
                : JsonSerializer.Deserialize<List<CalendarEventTypeDto>>(setting.Value) ?? [];
        }
        catch (JsonException)
        {
            storedTypes = [];
        }

        var result = storedTypes
            .Where(IsValidStoredType)
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(x => new CalendarEventTypeDto
            {
                Code = x.Code.Trim(),
                Name = x.Name.Trim(),
                Color = x.Color.Trim().ToLowerInvariant(),
                IsSystem = DefaultEventTypes.Any(defaultType =>
                    defaultType.Code.Equals(x.Code, StringComparison.OrdinalIgnoreCase))
            })
            .ToList();

        foreach (var defaultType in DefaultEventTypes)
        {
            if (result.All(x => !x.Code.Equals(defaultType.Code, StringComparison.OrdinalIgnoreCase)))
            {
                result.Insert(0, CloneEventType(defaultType));
            }
        }

        return result
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.IsSystem ? Array.FindIndex(DefaultEventTypes, d => d.Code == x.Code) : int.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task SaveEventTypesAsync(List<CalendarEventTypeDto> eventTypes)
    {
        var context = RequireContext();
        var setting = await context.SiteSettings.FirstOrDefaultAsync(x => x.Key == EventTypesSettingKey);
        var json = JsonSerializer.Serialize(eventTypes);

        if (setting == null)
        {
            context.SiteSettings.Add(new SiteSetting
            {
                Key = EventTypesSettingKey,
                Value = json,
                Description = "Calendar event types and their display colors.",
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = json;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    private static bool IsValidStoredType(CalendarEventTypeDto eventType) =>
        !string.IsNullOrWhiteSpace(eventType.Code) &&
        eventType.Code.Trim().Length <= 40 &&
        !string.IsNullOrWhiteSpace(eventType.Name) &&
        eventType.Name.Trim().Length <= 80 &&
        HexColorPattern.IsMatch(eventType.Color?.Trim() ?? string.Empty);

    private static string? ValidateEventTypeRequest(CalendarEventTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Името на типа е задължително.";

        if (request.Name.Trim().Length > 40)
            return "Името на типа може да бъде до 40 символа.";

        if (!HexColorPattern.IsMatch(request.Color?.Trim() ?? string.Empty))
            return "Цветът трябва да бъде във формат #RRGGBB.";

        return null;
    }

    private static CalendarEventTypeDto CloneEventType(CalendarEventTypeDto source) => new()
    {
        Code = source.Code,
        Name = source.Name,
        Color = source.Color,
        IsSystem = source.IsSystem
    };
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

public class CalendarEventTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563eb";
}

public class CalendarEventTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563eb";
    public bool IsSystem { get; set; }
}
