using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/testimonials")]
public class AdminTestimonialsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminTestimonialsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _context.Testimonials.OrderBy(x => x.DisplayOrder).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Testimonial entity)
    {
        entity.CreatedAtUtc = DateTime.UtcNow;
        _context.Testimonials.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Testimonial model)
    {
        var entity = await _context.Testimonials.FindAsync(id);
        if (entity == null) return NotFound();
        entity.ClientName = model.ClientName;
        entity.ClientCompany = model.ClientCompany;
        entity.ClientRole = model.ClientRole;
        entity.Content = model.Content;
        entity.Rating = model.Rating;
        entity.IsPublished = model.IsPublished;
        entity.DisplayOrder = model.DisplayOrder;
        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Testimonials.FindAsync(id);
        if (entity == null) return NotFound();
        _context.Testimonials.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
