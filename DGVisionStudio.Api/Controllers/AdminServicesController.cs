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
    public async Task<IActionResult> GetAll() => Ok(await _context.Services.OrderBy(x => x.DisplayOrder).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Service entity)
    {
        entity.CreatedAtUtc = DateTime.UtcNow;
        _context.Services.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Service model)
    {
        var entity = await _context.Services.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Title = model.Title;
        entity.Description = model.Description;
        entity.ShortDescription = model.ShortDescription;
        entity.CoverImageUrl = model.CoverImageUrl;
        entity.DisplayOrder = model.DisplayOrder;
        entity.IsActive = model.IsActive;
        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Services.FindAsync(id);
        if (entity == null) return NotFound();
        _context.Services.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
