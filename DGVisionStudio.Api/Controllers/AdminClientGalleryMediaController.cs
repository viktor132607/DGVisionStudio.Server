using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/client-galleries/{galleryId:int}/media")]
public class AdminClientGalleryMediaController : ControllerBase
{
    private readonly IAdminGalleryMediaManagementService _service;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleryMediaController(IAdminGalleryMediaManagementService service)
    {
        _service = service;
    }

    public AdminClientGalleryMediaController(AppDbContext context)
        : this(new AdminGalleryMediaMetadataService(context))
    {
    }

    [HttpPut("{mediaId:int}/metadata")]
    public async Task<IActionResult> UpdateMetadata(
        [FromRoute] int galleryId,
        [FromRoute] int mediaId,
        [FromBody] UpdateGalleryMediaMetadataRequest request) =>
        this.ToActionResult(await _service.UpdateMetadataAsync(galleryId, mediaId, request));
}

public class UpdateGalleryMediaMetadataRequest
{
    public string? Name { get; set; }
    public bool ClearAltAndCaption { get; set; }
}
