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
            return Unauthorized();

        var export = await privacyService.ExportUserDataAsync(user.Id);
        return export is null ? NotFound() : Ok(export);
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        if (!request.Confirm)
            return BadRequest(new { message = "Account deletion must be explicitly confirmed." });

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var anonymized = await privacyService.AnonymizeUserDataAsync(user.Id);
        return anonymized ? NoContent() : NotFound();
    }
}
