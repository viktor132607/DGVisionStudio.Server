using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;

    [ActivatorUtilitiesConstructor]
    public AuthController(IAuthService service)
    {
        _service = service;
    }

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
        : this(new AuthService(
            userManager,
            signInManager,
            emailService,
            configuration,
            NullLogger<AuthService>.Instance))
    {
    }

    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest model) =>
        this.ToActionResult(await _service.RegisterAsync(model, HttpContext.TraceIdentifier));

    [HttpGet("confirm-email")]
    public IActionResult ConfirmEmail() =>
        Redirect(_service.GetConfirmEmailRedirectUrl());

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest model) =>
        this.ToActionResult(await _service.LoginAsync(model, HttpContext.TraceIdentifier));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout() =>
        this.ToActionResult(await _service.LogoutAsync(CreateAuthContext()));

    [HttpGet("me")]
    public async Task<IActionResult> Me() =>
        this.ToActionResult(await _service.GetCurrentUserAsync(CreateAuthContext()));

    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model) =>
        this.ToActionResult(await _service.ForgotPasswordAsync(model, HttpContext.TraceIdentifier));

    [HttpGet("reset-password")]
    public IActionResult ResetPasswordPage([FromQuery] string email, [FromQuery] string token) =>
        Redirect(_service.GetResetPasswordRedirectUrl(email, token));

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model) =>
        this.ToActionResult(await _service.ResetPasswordAsync(model, HttpContext.TraceIdentifier));

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model) =>
        this.ToActionResult(await _service.ChangePasswordAsync(model, CreateAuthContext()));

    private AuthRequestContext CreateAuthContext() =>
        new(User, HttpContext.TraceIdentifier);
}
