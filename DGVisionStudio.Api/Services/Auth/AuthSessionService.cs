using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthSessionService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AuthSessionService> logger)
{
    public async Task<ControllerServiceResult> LoginAsync(
        LoginRequest model,
        string traceId)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Password))
        {
            logger.LogWarning(
                "Login failed because required fields are missing. TraceId: {TraceId}",
                traceId);
            return ControllerServiceResult.BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            logger.LogWarning(
                "Login failed for non-existing email. Email: {Email}, TraceId: {TraceId}",
                model.Email,
                traceId);
            return ControllerServiceResult.Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (user.IsBlocked)
        {
            logger.LogWarning(
                "Blocked user login attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                traceId);
            return ControllerServiceResult.Unauthorized(new
            {
                message = "Your account is blocked."
            });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning(
                "Locked out user login attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                traceId);
            return ControllerServiceResult.Locked(new
            {
                message = "Account is temporarily locked. Please try again later."
            });
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            false,
            true);

        if (result.IsLockedOut)
        {
            logger.LogWarning(
                "User locked out after failed login. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                traceId);
            return ControllerServiceResult.Locked(new
            {
                message = "Account is temporarily locked. Please try again later."
            });
        }

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Login failed. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                traceId);
            return ControllerServiceResult.Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        logger.LogInformation(
            "Login successful. UserId: {UserId}, Email: {Email}, Roles: {Roles}, TraceId: {TraceId}",
            user.Id,
            user.Email,
            roles,
            traceId);

        return ControllerServiceResult.Ok(new
        {
            message = "Login successful.",
            email = user.Email,
            roles
        });
    }

    public async Task<ControllerServiceResult> LogoutAsync(AuthRequestContext context)
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

    public async Task<ControllerServiceResult> GetCurrentUserAsync(AuthRequestContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return ControllerServiceResult.Ok(new { isAuthenticated = false });

        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
            return ControllerServiceResult.Ok(new { isAuthenticated = false });

        var roles = await userManager.GetRolesAsync(user);
        return ControllerServiceResult.Ok(new
        {
            isAuthenticated = true,
            email = user.Email,
            roles
        });
    }
}