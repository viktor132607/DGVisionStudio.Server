using System.Net;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<ControllerServiceResult> RegisterAsync(RegisterRequest model, string traceId)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Password) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            logger.LogWarning("Registration failed because required fields are missing. TraceId: {TraceId}", traceId);
            return ControllerServiceResult.BadRequest(new { message = "Email, password and confirm password are required." });
        }

        if (model.Password != model.ConfirmPassword)
        {
            logger.LogWarning("Registration failed because passwords do not match. Email: {Email}, TraceId: {TraceId}", model.Email, traceId);
            return ControllerServiceResult.BadRequest(new { message = "Passwords do not match." });
        }

        var normalizedEmail = model.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            logger.LogWarning("Registration failed because email already exists. Email: {Email}, TraceId: {TraceId}", normalizedEmail, traceId);
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
        logger.LogInformation("User registered successfully. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
        return ControllerServiceResult.Ok(new { message = "Registration successful." });
    }

    public string GetConfirmEmailRedirectUrl()
    {
        var frontendUrl = (configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{frontendUrl}/identity/login";
    }

    public async Task<ControllerServiceResult> LoginAsync(LoginRequest model, string traceId)
    {
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            logger.LogWarning("Login failed because required fields are missing. TraceId: {TraceId}", traceId);
            return ControllerServiceResult.BadRequest(new { message = "Email and password are required." });
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            logger.LogWarning("Login failed for non-existing email. Email: {Email}, TraceId: {TraceId}", model.Email, traceId);
            return ControllerServiceResult.Unauthorized(new { message = "Invalid email or password." });
        }

        if (user.IsBlocked)
        {
            logger.LogWarning("Blocked user login attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
            return ControllerServiceResult.Unauthorized(new { message = "Your account is blocked." });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Locked out user login attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
            return ControllerServiceResult.Locked(new { message = "Account is temporarily locked. Please try again later." });
        }

        var result = await signInManager.PasswordSignInAsync(user.UserName!, model.Password, false, true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("User locked out after failed login. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
            return ControllerServiceResult.Locked(new { message = "Account is temporarily locked. Please try again later." });
        }

        if (!result.Succeeded)
        {
            logger.LogWarning("Login failed. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
            return ControllerServiceResult.Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        logger.LogInformation("Login successful. UserId: {UserId}, Email: {Email}, Roles: {Roles}, TraceId: {TraceId}", user.Id, user.Email, roles, traceId);

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
        logger.LogInformation("Logout successful. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user?.Id, user?.Email, context.TraceId);
        return ControllerServiceResult.Ok(new { message = "Logged out successfully." });
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

    public async Task<ControllerServiceResult> ForgotPasswordAsync(ForgotPasswordRequest model, string traceId)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            logger.LogWarning("Password reset request failed because email is missing. TraceId: {TraceId}", traceId);
            return ControllerServiceResult.BadRequest(new { message = "Email is required." });
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            logger.LogInformation("Password reset requested for non-existing email. Email: {Email}, TraceId: {TraceId}", model.Email, traceId);
            return ControllerServiceResult.Ok(new { message = "If an account with that email exists, a reset link has been sent." });
        }

        if (user.IsBlocked)
        {
            logger.LogWarning("Password reset requested by blocked user. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
            return ControllerServiceResult.Ok(new { message = "If an account with that email exists, a reset link has been sent." });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var apiUrl = (configuration["Api:Url"] ?? "http://localhost:10000").TrimEnd('/');
        var resetUrl = $"{apiUrl}/api/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);

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

        logger.LogInformation("Password reset requested. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
        return ControllerServiceResult.Ok(new { message = "If an account with that email exists, a reset link has been sent." });
    }

    public string GetResetPasswordRedirectUrl(string email, string token)
    {
        var frontendUrl = (configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return $"{frontendUrl}/identity/login";

        return $"{frontendUrl}/identity/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    public async Task<ControllerServiceResult> ResetPasswordAsync(ResetPasswordRequest model, string traceId)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Token) ||
            string.IsNullOrWhiteSpace(model.Password) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            logger.LogWarning("Password reset failed because request is invalid. Email: {Email}, TraceId: {TraceId}", model.Email, traceId);
            return ControllerServiceResult.BadRequest(new { message = "Invalid reset request." });
        }

        if (model.Password != model.ConfirmPassword)
        {
            logger.LogWarning("Password reset failed because passwords do not match. Email: {Email}, TraceId: {TraceId}", model.Email, traceId);
            return ControllerServiceResult.BadRequest(new { message = "Passwords do not match." });
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            logger.LogWarning("Password reset failed for non-existing email. Email: {Email}, TraceId: {TraceId}", model.Email, traceId);
            return ControllerServiceResult.BadRequest(new { message = "Invalid reset request." });
        }

        var result = await userManager.ResetPasswordAsync(user, Uri.UnescapeDataString(model.Token), model.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Password reset failed by Identity validation. UserId: {UserId}, Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                result.Errors.Select(e => e.Description),
                traceId);

            return ControllerServiceResult.BadRequest(new
            {
                message = "Password reset failed.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        logger.LogInformation("Password reset successful. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, traceId);
        return ControllerServiceResult.Ok(new { message = "Password reset successful." });
    }

    public async Task<ControllerServiceResult> ChangePasswordAsync(ChangePasswordRequest model, AuthRequestContext context)
    {
        if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
            string.IsNullOrWhiteSpace(model.NewPassword) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            return ControllerServiceResult.BadRequest(new { message = "Current password, new password and confirm password are required." });
        }

        if (model.NewPassword != model.ConfirmPassword)
            return ControllerServiceResult.BadRequest(new { message = "Passwords do not match." });

        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
            return ControllerServiceResult.Unauthorized(new { message = "User not authenticated." });

        if (user.IsBlocked)
        {
            logger.LogWarning("Blocked user password change attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, context.TraceId);
            return ControllerServiceResult.Unauthorized(new { message = "Your account is blocked." });
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Password change failed. UserId: {UserId}, Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
                user.Id,
                user.Email,
                result.Errors.Select(e => e.Description),
                context.TraceId);

            return ControllerServiceResult.BadRequest(new
            {
                message = "Password change failed.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        logger.LogInformation("Password changed successfully. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, context.TraceId);
        return ControllerServiceResult.Ok(new { message = "Password changed successfully." });
    }
}
