using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleriesArchiveController : ControllerBase
{
    private readonly IAdminGalleryArchiveService _service;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleriesArchiveController(IAdminGalleryArchiveService service)
    {
        _service = service;
    }

    public AdminClientGalleriesArchiveController(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminClientGalleriesArchiveController> logger)
        : this(new AdminGalleryArchiveService(
            dbContext,
            fileStorageService,
            NullLogger<AdminGalleryArchiveService>.Instance))
    {
    }

    [HttpGet("download-all-file")]
    public async Task<IActionResult> DownloadAllAlbumsFile(CancellationToken cancellationToken)
    {
        var result = await _service.CreatePhysicalArchiveAsync(cancellationToken);
        if (result.IsSuccess && result.Value is PhysicalFileDownloadResult file)
        {
            Response.OnCompleted(file.CleanupAsync);
            return PhysicalFile(file.Path, file.ContentType, file.FileName);
        }

        return result.StatusCode == StatusCodes.Status204NoContent
            ? new EmptyResult()
            : this.ToActionResult(result);
    }
}
