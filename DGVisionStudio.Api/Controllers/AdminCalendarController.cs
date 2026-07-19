using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/calendar")]
public class AdminCalendarController : ControllerBase
{
    private readonly IAdminCalendarService _service;

    [ActivatorUtilitiesConstructor]
    public AdminCalendarController(IAdminCalendarService service)
    {
        _service = service;
    }

    public AdminCalendarController(AppDbContext context)
        : this(new AdminCalendarService(context))
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
