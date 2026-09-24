using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class AuthPasswordService
{
    private readonly AuthForgotPasswordService forgotPasswords;
    private readonly AuthPasswordResetLinkService resetLinks;
    private readonly AuthResetPasswordService resets;
    private readonly AuthChangePasswordService changes;

    [ActivatorUtilitiesConstructor]
    public AuthPasswordService(
        AuthForgotPasswordService forgotPasswords,
        AuthPasswordResetLinkService resetLinks,
        AuthResetPasswordService resets,
        AuthChangePasswordService changes)
    {
        this.forgotPasswords = forgotPasswords;
        this.resetLinks = resetLinks;
        this.resets = resets;
        this.changes = changes;
    }

    public AuthPasswordService(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthPasswordService> logger)
    {
        _ = logger;

        resetLinks = new AuthPasswordResetLinkService(configuration);
        forgotPasswords = new AuthForgotPasswordService(
            userManager,
            emailService,
            resetLinks,
            NullLogger<AuthForgotPasswordService>.Instance);
        resets = new AuthResetPasswordService(
            userManager,
            NullLogger<AuthResetPasswordService>.Instance);
        changes = new AuthChangePasswordService(
            userManager,
            NullLogger<AuthChangePasswordService>.Instance);
    }

    public Task<ControllerServiceResult> ForgotPasswordAsync(
        ForgotPasswordRequest model,
        string traceId) =>
        forgotPasswords.ForgotPasswordAsync(model, traceId);

    public string GetResetPasswordRedirectUrl(string email, string token) =>
        resetLinks.GetFrontendRedirectUrl(email, token);

    public Task<ControllerServiceResult> ResetPasswordAsync(
        ResetPasswordRequest model,
        string traceId) =>
        resets.ResetPasswordAsync(model, traceId);

    public Task<ControllerServiceResult> ChangePasswordAsync(
        ChangePasswordRequest model,
        AuthRequestContext context) =>
        changes.ChangePasswordAsync(model, context);
}
