using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthResetPasswordService(
    UserManager<ApplicationUser> userManager,
    ILogger<AuthResetPasswordService> logger)
{
    public async Task<ControllerServiceResult> ResetPasswordAsync(
        ResetPasswordRequest model,
        string traceId)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Token) ||
            string.IsNullOrWhiteSpace(model.Password) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            logger.LogWarning(
                "Password reset failed because request is invalid. Email: {Email}, TraceId: {TraceId}",
                model.Email,
                traceId);

            return InvalidResetRequest();
        }

        if (model.Password != model.ConfirmPassword)
        {
            logger.LogWarning(
                "Password reset failed because passwords do not match. Email: {Email}, TraceId: {TraceId}",
                model.Email,
                traceId);

            return ControllerServiceResult.BadRequest(
                new { message = "Passwords do not match." });
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            logger.LogWarning(
                "Password reset failed for non-existing email. Email: {Email}, TraceId: {TraceId}",
                model.Email,
                traceId);

            return InvalidResetRequest();
        }

        var result = await userManager.ResetPasswordAsync(
            user,
            Uri.UnescapeDataString(model.Token),
            model.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .Select(error => error.Description)
                .ToArray();

            logger.LogWarning(
                "Password reset failed by Identity validation. UserId: {UserId}, Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                errors,
                traceId);

            return ControllerServiceResult.BadRequest(new
            {
                message = "Password reset failed.",
                errors
            });
        }

        logger.LogInformation(
            "Password reset successful. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
            user.Id,
            user.Email,
            traceId);

        return ControllerServiceResult.Ok(
            new { message = "Password reset successful." });
    }

    private static ControllerServiceResult InvalidResetRequest() =>
        ControllerServiceResult.BadRequest(
            new { message = "Invalid reset request." });
}
