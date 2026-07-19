using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthRegistrationService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<AuthRegistrationService> logger)
{
    public async Task<ControllerServiceResult> RegisterAsync(
        RegisterRequest model,
        string traceId)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Password) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            logger.LogWarning(
                "Registration failed because required fields are missing. TraceId: {TraceId}",
                traceId);
            return ControllerServiceResult.BadRequest(new
            {
                message = "Email, password and confirm password are required."
            });
        }

        if (model.Password != model.ConfirmPassword)
        {
            logger.LogWarning(
                "Registration failed because passwords do not match. Email: {Email}, TraceId: {TraceId}",
                model.Email,
                traceId);
            return ControllerServiceResult.BadRequest(new { message = "Passwords do not match." });
        }

        var normalizedEmail = model.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            logger.LogWarning(
                "Registration failed because email already exists. Email: {Email}, TraceId: {TraceId}",
                normalizedEmail,
                traceId);
            return ControllerServiceResult.BadRequest(new { message = "Registration failed." });
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Registration failed by Identity validation. Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
                normalizedEmail,
                result.Errors.Select(e => e.Description),
                traceId);

            return ControllerServiceResult.BadRequest(new
            {
                message = "Registration failed.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        await userManager.AddToRoleAsync(user, "User");
        logger.LogInformation(
            "User registered successfully. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}",
            user.Id,
            user.Email,
            traceId);

        return ControllerServiceResult.Ok(new { message = "Registration successful." });
    }

    public string GetConfirmEmailRedirectUrl()
    {
        var frontendUrl = (configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{frontendUrl}/identity/login";
    }
}