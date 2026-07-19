using System.Security.Claims;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AccountEndpointService : IAccountEndpointService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountEndpointService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<ControllerServiceResult> DeleteAccountAsync(
        ClaimsPrincipal principal,
        string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return ControllerServiceResult.BadRequest(new { message = "Password is required." });

        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return ControllerServiceResult.Unauthorized(new { message = "User not authenticated." });

        if (!await _userManager.CheckPasswordAsync(user, password))
            return ControllerServiceResult.BadRequest(new { message = "Invalid password." });

        await _signInManager.SignOutAsync();
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "Account deletion failed.",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        return ControllerServiceResult.Ok(new { message = "Account deleted successfully." });
    }
}
