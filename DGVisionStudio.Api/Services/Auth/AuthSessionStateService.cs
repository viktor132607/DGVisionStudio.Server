using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthSessionStateService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AuthSessionService> logger)
{
    public async Task<ControllerServiceResult> LogoutAsync(
        AuthRequestContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        await signInManager.SignOutAsync();

        logger.LogInformation(
            "Logout successful. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
            user?.Id,
            user?.Email,
            context.TraceId);

        return ControllerServiceResult.Ok(new
        {
            message = "Logged out successfully."
        });
    }

    public async Task<ControllerServiceResult> GetCurrentUserAsync(
        AuthRequestContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return ControllerServiceResult.Ok(
                new { isAuthenticated = false });
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            return ControllerServiceResult.Ok(
                new { isAuthenticated = false });
        }

        var roles = await userManager.GetRolesAsync(user);

        return ControllerServiceResult.Ok(new
        {
            isAuthenticated = true,
            email = user.Email,
            roles
        });
    }
}
