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
    private const string EventPricesSettingKey = "CalendarEventPrices";
    private const string DefaultEventColor = "#64748b";
    private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

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
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();

        if (result.StatusCode == StatusCodes.Status200OK &&
            result.Value is IEnumerable<CalendarEvent> items)
        {
            var prices = await LoadEventPricesAsync();
            return Ok(items.Select(item => ToResponse(item, GetPrice(prices, item.Id))));
        }

        return this.ToActionResult(result);
    }

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
        var nextCode = request.Name.Trim();

        if (eventTypes
            .Where((_, itemIndex) => itemIndex != index)
            .Any(item => item.Code.Equals(nextCode, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Вече съществува тип с това име." });
        }

        var nextColor = request.Color.Trim().ToLowerInvariant();
        var updated = new CalendarEventTypeDto
        {
            Code = nextCode,
            Name = nextCode,
            Color = nextColor,
            IsSystem = false
        };
        eventTypes[index] = updated;

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
        return Ok(updated);
    }

    [HttpDelete("event-types/{code}")]
    public async Task<IActionResult> DeleteEventType(string code)
    {
        var context = RequireContext();
        var eventTypes = await LoadEventTypesAsync();
        var eventType = eventTypes.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        if (eventType == null)
            return NotFound();

        eventTypes.Remove(eventType);
        var replacement = eventTypes.FirstOrDefault();

        var matchingEvents = await context.CalendarEvents
            .Where(x => x.EventType == eventType.Code)
            .ToListAsync();

        foreach (var calendarEvent in matchingEvents)
        {
            calendarEvent.EventType = replacement?.Code;
            calendarEvent.Color = replacement?.Color;
            calendarEvent.UpdatedAtUtc = DateTime.UtcNow;
        }

        await SaveEventTypesAsync(eventTypes);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await _service.GetAsync(id);

        if (result.StatusCode == StatusCodes.Status200OK && result.Value is CalendarEvent item)
        {
            var prices = await LoadEventPricesAsync();
            return Ok(ToResponse(item, GetPrice(prices, item.Id)));
        }

        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CalendarEventDto dto)
    {
        if (dto.Price is < 0)
            return BadRequest(new { message = "Цената не може да бъде отрицателна." });

        var result = await _service.CreateAsync(dto);

        if (result.StatusCode == StatusCodes.Status201Created && result.Value is CalendarEvent item)
        {
            await SetEventPriceAsync(item.Id, dto.Price);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, ToResponse(item, dto.Price));
        }

        return this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CalendarEventDto dto)
    {
        if (dto.Price is < 0)
            return BadRequest(new { message = "Цената не може да бъде отрицателна." });

        var result = await _service.UpdateAsync(id, dto);

        if (result.StatusCode >= StatusCodes.Status200OK &&
            result.StatusCode < StatusCodes.Status300MultipleChoices)
        {
            await SetEventPriceAsync(id, dto.Price);

            if (result.Value is CalendarEvent item)
                return Ok(ToResponse(item, dto.Price));
        }

        return this.ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);

        if (result.StatusCode >= StatusCodes.Status200OK &&
            result.StatusCode < StatusCodes.Status300MultipleChoices)
        {
            await RemoveEventPriceAsync(id);
        }

        return this.ToActionResult(result);
    }

    private AppDbContext RequireContext() =>
        _context ?? throw new InvalidOperationException("Calendar settings storage is unavailable.");

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
                IsSystem = false
            })
            .OrderBy(x => x.Name)
            .ToList();

        if (result.Count > 0)
            return result;

        var existingEventTypes = await context.CalendarEvents
            .AsNoTracking()
            .Where(x => x.EventType != null && x.EventType != string.Empty)
            .Select(x => new { x.EventType, x.Color })
            .ToListAsync();

        result = existingEventTypes
            .GroupBy(x => x.EventType!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var color = group
                    .Select(x => x.Color?.Trim())
                    .FirstOrDefault(x => HexColorPattern.IsMatch(x ?? string.Empty))
                    ?.ToLowerInvariant() ?? DefaultEventColor;

                var name = group.Key.Trim();
                return new CalendarEventTypeDto
                {
                    Code = name,
                    Name = name,
                    Color = color,
                    IsSystem = false
                };
            })
            .OrderBy(x => x.Name)
            .ToList();

        if (result.Count > 0)
            await SaveEventTypesAsync(result);

        return result;
    }

    private async Task SaveEventTypesAsync(List<CalendarEventTypeDto> eventTypes)
    {
        var context = RequireContext();
        var normalizedTypes = eventTypes
            .Where(IsValidStoredType)
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(x => new CalendarEventTypeDto
            {
                Code = x.Code.Trim(),
                Name = x.Name.Trim(),
                Color = x.Color.Trim().ToLowerInvariant(),
                IsSystem = false
            })
            .OrderBy(x => x.Name)
            .ToList();
        var setting = await context.SiteSettings.FirstOrDefaultAsync(x => x.Key == EventTypesSettingKey);
        var json = JsonSerializer.Serialize(normalizedTypes);

        if (setting == null)
        {
            context.SiteSettings.Add(new SiteSetting
            {
                Key = EventTypesSettingKey,
                Value = json,
                Description = "Editable calendar event types and their display colors.",
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

    private async Task<Dictionary<int, decimal>> LoadEventPricesAsync()
    {
        if (_context == null)
            return [];

        var setting = await _context.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == EventPricesSettingKey);

        try
        {
            return setting == null
                ? []
                : (JsonSerializer.Deserialize<Dictionary<int, decimal>>(setting.Value) ?? [])
                    .Where(x => x.Value >= 0)
                    .ToDictionary(x => x.Key, x => x.Value);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveEventPricesAsync(Dictionary<int, decimal> prices)
    {
        if (_context == null)
            return;

        var setting = await _context.SiteSettings.FirstOrDefaultAsync(x => x.Key == EventPricesSettingKey);
        var json = JsonSerializer.Serialize(prices);

        if (setting == null)
        {
            _context.SiteSettings.Add(new SiteSetting
            {
                Key = EventPricesSettingKey,
                Value = json,
                Description = "Prices assigned to calendar events, indexed by event id.",
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = json;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task SetEventPriceAsync(int eventId, decimal? price)
    {
        if (_context == null)
            return;

        var prices = await LoadEventPricesAsync();

        if (price.HasValue)
            prices[eventId] = price.Value;
        else
            prices.Remove(eventId);

        await SaveEventPricesAsync(prices);
    }

    private async Task RemoveEventPriceAsync(int eventId)
    {
        if (_context == null)
            return;

        var prices = await LoadEventPricesAsync();
        if (!prices.Remove(eventId))
            return;

        await SaveEventPricesAsync(prices);
    }

    private static decimal? GetPrice(IReadOnlyDictionary<int, decimal> prices, int eventId) =>
        prices.TryGetValue(eventId, out var price) ? price : null;

    private static CalendarEventResponseDto ToResponse(CalendarEvent item, decimal? price) => new()
    {
        Id = item.Id,
        Title = item.Title,
        EventType = item.EventType,
        AssignedTo = item.AssignedTo,
        ClientName = item.ClientName,
        ClientPhone = item.ClientPhone,
        ClientEmail = item.ClientEmail,
        ContactRequestId = item.ContactRequestId,
        Location = item.Location,
        Description = item.Description,
        Color = item.Color,
        Price = price,
        RemindersEnabled = item.RemindersEnabled,
        Reminder24hSentAtUtc = item.Reminder24hSentAtUtc,
        Reminder2hSentAtUtc = item.Reminder2hSentAtUtc,
        StartAtUtc = item.StartAtUtc,
        EndAtUtc = item.EndAtUtc,
        CreatedAtUtc = item.CreatedAtUtc,
        UpdatedAtUtc = item.UpdatedAtUtc
    };

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
    public decimal? Price { get; set; }
    public bool RemindersEnabled { get; set; } = true;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
}

public class CalendarEventResponseDto
{
    public int Id { get; set; }
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
    public decimal? Price { get; set; }
    public bool RemindersEnabled { get; set; }
    public DateTime? Reminder24hSentAtUtc { get; set; }
    public DateTime? Reminder2hSentAtUtc { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class CalendarEventTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";
}

public class CalendarEventTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";
    public bool IsSystem { get; set; }
}
