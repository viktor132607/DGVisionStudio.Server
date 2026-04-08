using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SiteSettingsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _context.SiteSettings
            .OrderBy(x => x.Key)
            .Select(x => new { x.Key, x.Value, x.Description, x.UpdatedAtUtc })
            .ToListAsync();

        return Ok(settings);
    }
}
