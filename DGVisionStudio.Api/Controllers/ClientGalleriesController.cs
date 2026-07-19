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

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/client-galleries")]
[Authorize]
public class ClientGalleriesController : ControllerBase
{
    private readonly IClientGalleryEndpointService _service;

    [ActivatorUtilitiesConstructor]
    public ClientGalleriesController(IClientGalleryEndpointService service)
    {
        _service = service;
    }

    public ClientGalleriesController(
        IClientGalleryService clientGalleryService,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IFileStorageService fileStorageService)
        : this(new ClientGalleryEndpointService(
            clientGalleryService,
            userManager,
            dbContext,
            fileStorageService))
    {
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyGalleries() =>
        this.ToActionResult(await _service.GetMyGalleriesAsync(User));

    [HttpPost("my")]
    public async Task<IActionResult> CreateMyGallery([FromBody] CreateUserClientGalleryRequest request) =>
        this.ToActionResult(await _service.CreateMyGalleryAsync(User, request));

    [HttpGet("{galleryId:int}")]
    public async Task<IActionResult> GetGalleryDetails([FromRoute] int galleryId) =>
        this.ToActionResult(await _service.GetGalleryDetailsAsync(User, galleryId));

    [HttpPost("{galleryId:int}/photos/upload")]
    public async Task<IActionResult> UploadMyGalleryPhoto(
        [FromRoute] int galleryId,
        IFormFile file) =>
        this.ToActionResult(await _service.UploadMyGalleryPhotoAsync(User, galleryId, file));

    [HttpDelete("{galleryId:int}")]
    public async Task<IActionResult> DeleteMyGallery([FromRoute] int galleryId) =>
        this.ToActionResult(await _service.DeleteMyGalleryAsync(User, galleryId));

    [HttpGet("{galleryId:int}/photos/{photoId:int}/download")]
    public async Task<IActionResult> DownloadPhoto([FromRoute] int galleryId, [FromRoute] int photoId) =>
        ToFileResult(await _service.DownloadPhotoAsync(User, galleryId, photoId));

    [HttpGet("{galleryId:int}/download")]
    public async Task<IActionResult> DownloadGalleryZip([FromRoute] int galleryId) =>
        ToFileResult(await _service.DownloadGalleryZipAsync(User, galleryId));

    private IActionResult ToFileResult(ControllerServiceResult result) =>
        result.IsSuccess && result.Value is FileDownloadResult file
            ? File(file.Stream, file.ContentType, file.FileName)
            : this.ToActionResult(result);
}
