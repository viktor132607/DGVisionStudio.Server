using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries/{galleryId:int}/publish-schedule")]
[Authorize(Roles = "Admin")]
public sealed class AdminGalleryPublishScheduleController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSchedule([FromRoute] int galleryId)
    {
        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Where(x => x.Id == galleryId)
            .Select(x => new
            {
                x.Id,
                x.PublishAtUtc
            })
            .FirstOrDefaultAsync();

        return album == null ? NotFound() : Ok(album);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSchedule(
        [FromRoute] int galleryId,
        [FromBody] UpdateAlbumPublishScheduleRequest request)
    {
        var album = await dbContext.PortfolioAlbums.FirstOrDefaultAsync(x => x.Id == galleryId);
        if (album == null)
            return NotFound();

        album.PublishAtUtc = request.PublishAtUtc?.UtcDateTime;
        await dbContext.SaveChangesAsync();

        return Ok(new
        {
            album.Id,
            album.PublishAtUtc
        });
    }
}

public sealed class UpdateAlbumPublishScheduleRequest
{
    public DateTimeOffset? PublishAtUtc { get; set; }
}
