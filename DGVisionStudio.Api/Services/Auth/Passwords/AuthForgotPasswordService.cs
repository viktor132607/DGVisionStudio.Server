using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthForgotPasswordService(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    AuthPasswordResetLinkService resetLinks,
    ILogger<AuthForgotPasswordService> logger)
{
    public async Task<ControllerServiceResult> ForgotPasswordAsync(
        ForgotPasswordRequest model,
        string traceId)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            logger.LogWarning(
                "Password reset request failed because email is missing. TraceId: {TraceId}",
                traceId);

            return ControllerServiceResult.BadRequest(
                new { message = "Email is required." });
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            logger.LogInformation(
                "Password reset requested for non-existing email. Email: {Email}, TraceId: {TraceId}",
                model.Email,
                traceId);

            return ResetRequestAccepted();
        }

        if (user.IsBlocked)
        {
            logger.LogWarning(
                "Password reset requested by blocked user. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                traceId);

            return ResetRequestAccepted();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var safeResetUrl = resetLinks.BuildSafeApiResetUrl(
            user.Email!,
            token);

        await emailService.SendAsync(
            user.Email!,
            "Reset your password",
            $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#111;">
                <p>You requested a password reset.</p>
                <p>Click the link below to set a new password:</p>
                <p><a href="{safeResetUrl}">Reset password</a></p>
            </div>
            """);

        logger.LogInformation(
            "Password reset requested. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
            user.Id,
            user.Email,
            traceId);

        return ResetRequestAccepted();
    }

    private static ControllerServiceResult ResetRequestAccepted() =>
        ControllerServiceResult.Ok(new
        {
            message =
                "If an account with that email exists, a reset link has been sent."
        });
}
