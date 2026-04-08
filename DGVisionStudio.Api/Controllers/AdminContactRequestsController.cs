using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/contact-requests")]
public class AdminContactRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminContactRequestsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.ContactRequests.OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequestDto dto)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        if (item == null) return NotFound();

        item.Status = dto.Status;
        item.AdminComment = dto.AdminComment;
        item.IsArchived = dto.IsArchived;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.ContactRequests.FindAsync(id);
        if (item == null) return NotFound();
        _context.ContactRequests.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
