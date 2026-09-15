using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/portfolio/albums")]
[Authorize(Roles = "Admin")]
public sealed class AdminPortfolioAlbumDownloadController(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<AdminPortfolioAlbumDownloadController> logger) : ControllerBase
{
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadAlbum(int id, CancellationToken cancellationToken)
    {
        try
        {
            var download = await new PortfolioArchiveBuilder(dbContext, fileStorageService)
                .BuildAsync([id], null, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            Response.OnCompleted(download.CleanupAsync);
            return PhysicalFile(download.Path, download.ContentType, download.FileName);
        }
        catch (ArchiveRequestException ex) { return NotFound(new { message = ex.Message }); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build album archive {AlbumId}", id);
            return StatusCode(500, new { message = "Архивът не можа да бъде създаден. Опитай отново." });
        }
    }
}
