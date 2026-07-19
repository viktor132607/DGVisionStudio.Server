using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleriesController : ControllerBase
{
    private readonly IAdminClientGalleryManagementService _service;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleriesController(IAdminClientGalleryManagementService service)
    {
        _service = service;
    }

    public AdminClientGalleriesController(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminClientGalleriesController> logger)
        : this(new AdminClientGalleryManagementService(
            clientGalleryService,
            auditLogService,
            userManager,
            dbContext,
            fileStorageService,
            NullLogger<AdminClientGalleryManagementService>.Instance))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAllGalleries() =>
        this.ToActionResult(await _service.GetAllGalleriesAsync());

    [HttpGet("download-all")]
    public async Task<IActionResult> DownloadAllAlbums()
    {
        var result = await _service.DownloadAllAlbumsAsync(this.CreateAdminRequestContext());
        if (result.IsSuccess && result.Value is FileDownloadResult file)
            return File(file.Stream, file.ContentType, file.FileName);

        return this.ToActionResult(result);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAvailableUsers() =>
        this.ToActionResult(await _service.GetAvailableUsersAsync());

    [HttpGet("{galleryId:int}")]
    public async Task<IActionResult> GetGalleryById([FromRoute] int galleryId) =>
        this.ToActionResult(await _service.GetGalleryByIdAsync(galleryId));

    [HttpPost]
    public async Task<IActionResult> CreateGallery([FromBody] AdminCreateClientGalleryRequest request) =>
        this.ToActionResult(await _service.CreateGalleryAsync(request, this.CreateAdminRequestContext()));

    [HttpPut("{galleryId:int}")]
    public async Task<IActionResult> UpdateGallery(
        [FromRoute] int galleryId,
        [FromBody] AdminUpdateClientGalleryRequest request) =>
        this.ToActionResult(await _service.UpdateGalleryAsync(galleryId, request, this.CreateAdminRequestContext()));

    [HttpDelete("{galleryId:int}")]
    public async Task<IActionResult> DeleteGallery([FromRoute] int galleryId) =>
        this.ToActionResult(await _service.DeleteGalleryAsync(galleryId, this.CreateAdminRequestContext()));
}
