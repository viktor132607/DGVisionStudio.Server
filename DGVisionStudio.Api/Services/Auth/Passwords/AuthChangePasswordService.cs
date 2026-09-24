using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthChangePasswordService(
    UserManager<ApplicationUser> userManager,
    ILogger<AuthChangePasswordService> logger)
{
    public async Task<ControllerServiceResult> ChangePasswordAsync(
        ChangePasswordRequest model,
        AuthRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
            string.IsNullOrWhiteSpace(model.NewPassword) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            return ControllerServiceResult.BadRequest(new
            {
                message =
                    "Current password, new password and confirm password are required."
            });
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Passwords do not match." });
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            return ControllerServiceResult.Unauthorized(
                new { message = "User not authenticated." });
        }

        if (user.IsBlocked)
        {
            logger.LogWarning(
                "Blocked user password change attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                context.TraceId);

            return ControllerServiceResult.Unauthorized(
                new { message = "Your account is blocked." });
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .Select(error => error.Description)
                .ToArray();

            logger.LogWarning(
                "Password change failed. UserId: {UserId}, Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                errors,
                context.TraceId);

            return ControllerServiceResult.BadRequest(new
            {
                message = "Password change failed.",
                errors
            });
        }

        logger.LogInformation(
            "Password changed successfully. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
            user.Id,
            user.Email,
            context.TraceId);

        return ControllerServiceResult.Ok(
            new { message = "Password changed successfully." });
    }
}
