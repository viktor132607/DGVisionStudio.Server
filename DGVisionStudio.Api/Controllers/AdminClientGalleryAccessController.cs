using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries/{galleryId:int}/access")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleryAccessController : ControllerBase
{
    private readonly IAdminGalleryAccessEndpointService _service;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleryAccessController(IAdminGalleryAccessEndpointService service)
    {
        _service = service;
    }

    public AdminClientGalleryAccessController(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminClientGalleryAccessController> logger)
        : this(new AdminGalleryAccessEndpointService(
            clientGalleryService,
            auditLogService,
            NullLogger<AdminGalleryAccessEndpointService>.Instance))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetGalleryAccesses([FromRoute] int galleryId) =>
        this.ToActionResult(await _service.GetGalleryAccessesAsync(galleryId));

    [HttpPost]
    public async Task<IActionResult> GrantAccess(
        [FromRoute] int galleryId,
        [FromBody] GrantGalleryAccessRequest request) =>
        this.ToActionResult(await _service.GrantAccessAsync(
            galleryId,
            request,
            this.CreateAdminRequestContext()));

    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateAccess(
        [FromRoute] int galleryId,
        [FromRoute] string userId,
        [FromBody] UpdateGalleryAccessRequest request) =>
        this.ToActionResult(await _service.UpdateAccessAsync(
            galleryId,
            userId,
            request,
            this.CreateAdminRequestContext()));

    [HttpDelete("{userId}")]
    public async Task<IActionResult> RemoveAccess(
        [FromRoute] int galleryId,
        [FromRoute] string userId) =>
        this.ToActionResult(await _service.RemoveAccessAsync(
            galleryId,
            userId,
            this.CreateAdminRequestContext()));
}
