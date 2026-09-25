using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AuthSessionService
{
    private readonly AuthLoginService _login;
    private readonly AuthSessionStateService _state;

    [ActivatorUtilitiesConstructor]
    public AuthSessionService(
        AuthLoginService login,
        AuthSessionStateService state)
    {
        _login = login;
        _state = state;
    }

    public AuthSessionService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthSessionService> logger)
        : this(
            new AuthLoginService(
                userManager,
                signInManager,
                logger),
            new AuthSessionStateService(
                userManager,
                signInManager,
                logger))
    {
    }

    public Task<ControllerServiceResult> LoginAsync(
        LoginRequest model,
        string traceId) =>
        _login.LoginAsync(model, traceId);

    public Task<ControllerServiceResult> LogoutAsync(
        AuthRequestContext context) =>
        _state.LogoutAsync(context);

    public Task<ControllerServiceResult> GetCurrentUserAsync(
        AuthRequestContext context) =>
        _state.GetCurrentUserAsync(context);
}
