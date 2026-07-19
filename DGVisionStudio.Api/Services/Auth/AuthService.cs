using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuthRegistrationService _registration;
    private readonly AuthSessionService _session;
    private readonly AuthPasswordService _passwords;

    [ActivatorUtilitiesConstructor]
    public AuthService(
        AuthRegistrationService registration,
        AuthSessionService session,
        AuthPasswordService passwords)
    {
        _registration = registration;
        _session = session;
        _passwords = passwords;
    }

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
        : this(
            new AuthRegistrationService(
                userManager,
                configuration,
                NullLogger<AuthRegistrationService>.Instance),
            new AuthSessionService(
                userManager,
                signInManager,
                NullLogger<AuthSessionService>.Instance),
            new AuthPasswordService(
                userManager,
                emailService,
                configuration,
                NullLogger<AuthPasswordService>.Instance))
    {
    }

    public Task<ControllerServiceResult> RegisterAsync(
        RegisterRequest model,
        string traceId) =>
        _registration.RegisterAsync(model, traceId);

    public string GetConfirmEmailRedirectUrl() =>
        _registration.GetConfirmEmailRedirectUrl();

    public Task<ControllerServiceResult> LoginAsync(
        LoginRequest model,
        string traceId) =>
        _session.LoginAsync(model, traceId);

    public Task<ControllerServiceResult> LogoutAsync(AuthRequestContext context) =>
        _session.LogoutAsync(context);

    public Task<ControllerServiceResult> GetCurrentUserAsync(AuthRequestContext context) =>
        _session.GetCurrentUserAsync(context);

    public Task<ControllerServiceResult> ForgotPasswordAsync(
        ForgotPasswordRequest model,
        string traceId) =>
        _passwords.ForgotPasswordAsync(model, traceId);

    public string GetResetPasswordRedirectUrl(string email, string token) =>
        _passwords.GetResetPasswordRedirectUrl(email, token);

    public Task<ControllerServiceResult> ResetPasswordAsync(
        ResetPasswordRequest model,
        string traceId) =>
        _passwords.ResetPasswordAsync(model, traceId);

    public Task<ControllerServiceResult> ChangePasswordAsync(
        ChangePasswordRequest model,
        AuthRequestContext context) =>
        _passwords.ChangePasswordAsync(model, context);
}