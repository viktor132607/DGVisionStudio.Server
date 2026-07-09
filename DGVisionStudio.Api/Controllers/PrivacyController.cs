using DGVisionStudio.Api.Models;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/me/privacy")]
public sealed class PrivacyController(
    UserManager<ApplicationUser> userManager,
    IPrivacyService privacyService) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return ApiError.Unauthorized(traceId: HttpContext.TraceIdentifier);

        var export = await privacyService.ExportUserDataAsync(user.Id);
        return export is null ? ApiError.NotFound(traceId: HttpContext.TraceIdentifier) : Ok(export);
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        if (!request.Confirm)
            return ApiError.Validation("Account deletion must be explicitly confirmed.", HttpContext.TraceIdentifier);

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return ApiError.Unauthorized(traceId: HttpContext.TraceIdentifier);

        var anonymized = await privacyService.AnonymizeUserDataAsync(user.Id);
        return anonymized ? NoContent() : ApiError.NotFound(traceId: HttpContext.TraceIdentifier);
    }
}
