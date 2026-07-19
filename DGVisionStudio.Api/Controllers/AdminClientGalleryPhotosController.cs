using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries/{galleryId:int}")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleryPhotosController : ControllerBase
{
    private const long MaxPhotoUploadRequestSizeBytes = 25 * 1024 * 1024;
    private const long MaxVideoUploadRequestSizeBytes = 105 * 1024 * 1024;

    private readonly IAdminGalleryMediaManagementService _service;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleryPhotosController(IAdminGalleryMediaManagementService service)
    {
        _service = service;
    }

    public AdminClientGalleryPhotosController(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminClientGalleryPhotosController> logger,
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        ClientGalleryMapper mapper)
        : this(new AdminGalleryMediaManagementService(
            clientGalleryService,
            auditLogService,
            NullLogger<AdminGalleryMediaManagementService>.Instance,
            dbContext,
            environment,
            mapper))
    {
    }

    [HttpGet("photos/{photoId:int}/download")]
    public async Task<IActionResult> DownloadPhoto(
        [FromRoute] int galleryId,
        [FromRoute] int photoId) =>
        ToFileResult(await _service.DownloadPhotoAsync(
            galleryId,
            photoId,
            this.CreateAdminRequestContext()));

    [RequestSizeLimit(MaxPhotoUploadRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoUploadRequestSizeBytes)]
    [HttpPost("photos/upload")]
    public async Task<IActionResult> UploadPhoto(
        [FromRoute] int galleryId,
        IFormFile file) =>
        this.ToActionResult(await _service.UploadPhotoAsync(
            galleryId,
            file,
            this.CreateAdminRequestContext()));

    [RequestSizeLimit(MaxVideoUploadRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVideoUploadRequestSizeBytes)]
    [HttpPost("videos/upload")]
    public async Task<IActionResult> UploadVideo(
        [FromRoute] int galleryId,
        IFormFile file) =>
        this.ToActionResult(await _service.UploadVideoAsync(
            galleryId,
            file,
            this.CreateAdminRequestContext()));

    [HttpPut("photos/{photoId:int}")]
    public async Task<IActionResult> UpdatePhoto(
        [FromRoute] int galleryId,
        [FromRoute] int photoId,
        [FromBody] UpdateClientPhotoRequest request) =>
        this.ToActionResult(await _service.UpdatePhotoAsync(
            galleryId,
            photoId,
            request,
            this.CreateAdminRequestContext()));

    [HttpDelete("photos/{photoId:int}")]
    public async Task<IActionResult> DeletePhoto(
        [FromRoute] int galleryId,
        [FromRoute] int photoId) =>
        this.ToActionResult(await _service.DeletePhotoAsync(
            galleryId,
            photoId,
            this.CreateAdminRequestContext()));

    [HttpPut("cover")]
    public async Task<IActionResult> SetCoverImage(
        [FromRoute] int galleryId,
        [FromBody] SetGalleryCoverRequest request) =>
        this.ToActionResult(await _service.SetCoverImageAsync(
            galleryId,
            request,
            this.CreateAdminRequestContext()));

    [HttpPut("photos/reorder")]
    public async Task<IActionResult> ReorderPhotos(
        [FromRoute] int galleryId,
        [FromBody] ReorderGalleryPhotosRequest request) =>
        this.ToActionResult(await _service.ReorderPhotosAsync(
            galleryId,
            request,
            this.CreateAdminRequestContext()));

    private IActionResult ToFileResult(ControllerServiceResult result) =>
        result.IsSuccess && result.Value is FileDownloadResult file
            ? File(file.Stream, file.ContentType, file.FileName)
            : this.ToActionResult(result);
}
