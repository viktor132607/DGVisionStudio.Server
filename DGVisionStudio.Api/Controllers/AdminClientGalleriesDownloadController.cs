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
public class AdminClientGalleriesDownloadController : ControllerBase
{
    private readonly IAdminGalleryArchiveService _service;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleriesDownloadController(IAdminGalleryArchiveService service)
    {
        _service = service;
    }

    public AdminClientGalleriesDownloadController(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminClientGalleriesDownloadController> logger)
        : this(new AdminGalleryArchiveService(
            dbContext,
            fileStorageService,
            NullLogger<AdminGalleryArchiveService>.Instance))
    {
    }

    [HttpGet("download-all-stream")]
    public async Task<IActionResult> DownloadAllAlbumsStream(CancellationToken cancellationToken)
    {
        var result = await _service.PrepareStreamingArchiveAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is not StreamingFileDownloadResult file)
            return this.ToActionResult(result);

        Response.ContentType = file.ContentType;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{file.FileName}\"";
        Response.Headers.CacheControl = "no-store";
        await file.WriteAsync(Response.Body, cancellationToken);
        return new EmptyResult();
    }
}
