using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/services")]
public class AdminServicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminServicesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _context.Services.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceCardDto dto)
    {
        var validationError = Validate(dto);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var maxOrder = await _context.Services.AnyAsync()
            ? await _context.Services.MaxAsync(x => x.DisplayOrder)
            : 0;

        var item = new Service
        {
            Title = dto.Title.Trim(),
            ShortDescription = Normalize(dto.ShortDescription),
            Description = dto.Description.Trim(),
            CoverImageUrl = Normalize(dto.CoverImageUrl),
            DisplayOrder = maxOrder + 1,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Services.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceCardDto dto)
    {
        var item = await _context.Services.FindAsync(id);
        if (item is null) return NotFound();

        var validationError = Validate(dto);
        if (validationError is not null) return BadRequest(new { message = validationError });

        item.Title = dto.Title.Trim();
        item.ShortDescription = Normalize(dto.ShortDescription);
        item.Description = dto.Description.Trim();
        item.CoverImageUrl = Normalize(dto.CoverImageUrl);
        item.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return Ok(item);
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderServicesDto dto)
    {
        if (dto.Ids.Count == 0) return BadRequest(new { message = "Няма подадени услуги за пренареждане." });

        var services = await _context.Services.ToListAsync();
        var byId = services.ToDictionary(x => x.Id);
        var order = 1;

        foreach (var id in dto.Ids.Distinct())
        {
            if (!byId.TryGetValue(id, out var item)) continue;
            item.DisplayOrder = order++;
        }

        foreach (var item in services
            .Where(x => !dto.Ids.Contains(x.Id))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id))
        {
            item.DisplayOrder = order++;
        }

        await _context.SaveChangesAsync();

        return Ok(await _context.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.Services.FindAsync(id);
        if (item is null) return NotFound();

        _context.Services.Remove(item);
        await _context.SaveChangesAsync();
        await NormalizeDisplayOrder();

        return NoContent();
    }

    private async Task NormalizeDisplayOrder()
    {
        var items = await _context.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
        {
            items[i].DisplayOrder = i + 1;
        }

        await _context.SaveChangesAsync();
    }

    private static string? Validate(ServiceCardDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return "Заглавието е задължително.";
        if (string.IsNullOrWhiteSpace(dto.Description)) return "Описанието е задължително.";
        return null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class ServiceCardDto
{
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ReorderServicesDto
{
    public List<int> Ids { get; set; } = [];
}
