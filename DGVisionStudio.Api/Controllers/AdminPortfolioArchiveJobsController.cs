using System.Security.Claims;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/portfolio/albums/archive-jobs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPortfolioArchiveJobsController(PortfolioArchiveJobs jobs) : ControllerBase
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    [HttpPost]
    public IActionResult Create(AlbumSelectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(Owner)) return Unauthorized();
        try
        {
            var job = jobs.Enqueue(Owner, request.AlbumIds);
            return AcceptedAtAction(nameof(Status), new { id = job.Id }, job);
        }
        catch (ArchiveRequestException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public IActionResult Status(Guid id) => jobs.Get(id, Owner) is { } job
        ? Ok(job) : NotFound(new { message = "Архивът е изтекъл или сървърът е рестартиран. Създай го отново." });

    [HttpGet("{id:guid}/download")]
    public IActionResult Download(Guid id)
    {
        var file = jobs.Open(id, Owner);
        return file is null ? NotFound(new { message = "Архивът не е готов или е изтекъл." })
            : File(file.Stream, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id) => await jobs.RemoveAsync(id, Owner) ? NoContent() : NotFound();
}
