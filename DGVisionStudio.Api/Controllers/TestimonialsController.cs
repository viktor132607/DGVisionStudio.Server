using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/testimonials")]
public class TestimonialsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TestimonialsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.Testimonials
            .Where(x => x.IsPublished)
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return Ok(items);
    }
}
