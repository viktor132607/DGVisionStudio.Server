using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Models;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/me/privacy")]
public sealed class PrivacyController : ControllerBase
{
    private readonly IPrivacyEndpointService _service;

    [ActivatorUtilitiesConstructor]
    public PrivacyController(IPrivacyEndpointService service)
    {
        _service = service;
    }

    public PrivacyController(
        UserManager<ApplicationUser> userManager,
        IPrivacyService privacyService)
        : this(new PrivacyEndpointService(userManager, privacyService))
    {
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export() =>
        this.ToActionResult(await _service.ExportAsync(User, HttpContext.TraceIdentifier));

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request) =>
        this.ToActionResult(await _service.DeleteAccountAsync(
            User,
            request.Confirm,
            HttpContext.TraceIdentifier));
}
