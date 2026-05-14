using System.Net;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly SignInManager<ApplicationUser> _signInManager;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;
	private readonly ILogger<AuthController> _logger;

	public AuthController(
		UserManager<ApplicationUser> userManager,
		SignInManager<ApplicationUser> signInManager,
		IEmailService emailService,
		IConfiguration configuration,
		ILogger<AuthController> logger)
	{
		_userManager = userManager;
		_signInManager = signInManager;
		_emailService = emailService;
		_configuration = configuration;
		_logger = logger;
	}

	[EnableRateLimiting("auth")]
	[HttpPost("register")]
	public async Task<IActionResult> Register([FromBody] RegisterRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email) ||
			string.IsNullOrWhiteSpace(model.Password) ||
			string.IsNullOrWhiteSpace(model.ConfirmPassword))
		{
			_logger.LogWarning("Registration failed because required fields are missing. TraceId: {TraceId}", HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Email, password and confirm password are required." });
		}

		if (model.Password != model.ConfirmPassword)
		{
			_logger.LogWarning("Registration failed because passwords do not match. Email: {Email}, TraceId: {TraceId}", model.Email, HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Passwords do not match." });
		}

		var normalizedEmail = model.Email.Trim();

		var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
		if (existingUser != null)
		{
			_logger.LogWarning("Registration failed because email already exists. Email: {Email}, TraceId: {TraceId}", normalizedEmail, HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Registration failed." });
		}

		var user = new ApplicationUser
		{
			UserName = normalizedEmail,
			Email = normalizedEmail,
			EmailConfirmed = true
		};

		var result = await _userManager.CreateAsync(user, model.Password);
		if (!result.Succeeded)
		{
			_logger.LogWarning(
				"Registration failed by Identity validation. Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
				normalizedEmail,
				result.Errors.Select(e => e.Description),
				HttpContext.TraceIdentifier);

			return BadRequest(new
			{
				message = "Registration failed.",
				errors = result.Errors.Select(e => e.Description)
			});
		}

		await _userManager.AddToRoleAsync(user, "User");

		_logger.LogInformation("User registered successfully. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);

		return Ok(new { message = "Registration successful." });
	}

	[HttpGet("confirm-email")]
	public async Task<IActionResult> ConfirmEmail()
	{
		var frontendUrl = (_configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');

		await Task.CompletedTask;
		return Redirect($"{frontendUrl}/identity/login");
	}

	[EnableRateLimiting("auth")]
	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
		{
			_logger.LogWarning("Login failed because required fields are missing. TraceId: {TraceId}", HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Email and password are required." });
		}

		var user = await _userManager.FindByEmailAsync(model.Email);
		if (user == null)
		{
			_logger.LogWarning("Login failed for non-existing email. Email: {Email}, TraceId: {TraceId}", model.Email, HttpContext.TraceIdentifier);
			return Unauthorized(new { message = "Invalid email or password." });
		}

		if (user.IsBlocked)
		{
			_logger.LogWarning("Blocked user login attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);
			return Unauthorized(new { message = "Your account is blocked." });
		}

		if (await _userManager.IsLockedOutAsync(user))
		{
			_logger.LogWarning("Locked out user login attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);
			return StatusCode(StatusCodes.Status423Locked, new { message = "Account is temporarily locked. Please try again later." });
		}

		var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, false, true);

		if (result.IsLockedOut)
		{
			_logger.LogWarning("User locked out after failed login. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);
			return StatusCode(StatusCodes.Status423Locked, new { message = "Account is temporarily locked. Please try again later." });
		}

		if (!result.Succeeded)
		{
			_logger.LogWarning("Login failed. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);
			return Unauthorized(new { message = "Invalid email or password." });
		}

		var roles = await _userManager.GetRolesAsync(user);

		_logger.LogInformation("Login successful. UserId: {UserId}, Email: {Email}, Roles: {Roles}, TraceId: {TraceId}", user.Id, user.Email, roles, HttpContext.TraceIdentifier);

		return Ok(new
		{
			message = "Login successful.",
			email = user.Email,
			roles
		});
	}

	[Authorize]
	[HttpPost("logout")]
	public async Task<IActionResult> Logout()
	{
		var user = await _userManager.GetUserAsync(User);

		await _signInManager.SignOutAsync();

		_logger.LogInformation("Logout successful. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user?.Id, user?.Email, HttpContext.TraceIdentifier);

		return Ok(new { message = "Logged out successfully." });
	}

	[HttpGet("me")]
	public async Task<IActionResult> Me()
	{
		if (User.Identity?.IsAuthenticated != true)
			return Ok(new { isAuthenticated = false });

		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Ok(new { isAuthenticated = false });

		var roles = await _userManager.GetRolesAsync(user);

		return Ok(new
		{
			isAuthenticated = true,
			email = user.Email,
			roles
		});
	}

	[EnableRateLimiting("auth")]
	[HttpPost("forgot-password")]
	public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email))
		{
			_logger.LogWarning("Password reset request failed because email is missing. TraceId: {TraceId}", HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Email is required." });
		}

		var user = await _userManager.FindByEmailAsync(model.Email);

		if (user == null)
		{
			_logger.LogInformation("Password reset requested for non-existing email. Email: {Email}, TraceId: {TraceId}", model.Email, HttpContext.TraceIdentifier);
			return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
		}

		if (user.IsBlocked)
		{
			_logger.LogWarning("Password reset requested by blocked user. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);
			return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
		}

		var token = await _userManager.GeneratePasswordResetTokenAsync(user);
		var apiUrl = (_configuration["Api:Url"] ?? "http://localhost:10000").TrimEnd('/');

		var resetUrl =
			$"{apiUrl}/api/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

		var safeResetUrl = WebUtility.HtmlEncode(resetUrl);

		await _emailService.SendAsync(
			user.Email!,
			"Reset your password",
			$"""
			<div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#111;">
				<p>You requested a password reset.</p>
				<p>Click the link below to set a new password:</p>
				<p><a href="{safeResetUrl}">Reset password</a></p>
			</div>
			""");

		_logger.LogInformation("Password reset requested. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);

		return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
	}

	[HttpGet("reset-password")]
	public IActionResult ResetPasswordPage([FromQuery] string email, [FromQuery] string token)
	{
		var frontendUrl = (_configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');

		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
			return Redirect($"{frontendUrl}/identity/login");

		return Redirect(
			$"{frontendUrl}/identity/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
	}

	[HttpPost("reset-password")]
	public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email) ||
			string.IsNullOrWhiteSpace(model.Token) ||
			string.IsNullOrWhiteSpace(model.Password) ||
			string.IsNullOrWhiteSpace(model.ConfirmPassword))
		{
			_logger.LogWarning("Password reset failed because request is invalid. Email: {Email}, TraceId: {TraceId}", model.Email, HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Invalid reset request." });
		}

		if (model.Password != model.ConfirmPassword)
		{
			_logger.LogWarning("Password reset failed because passwords do not match. Email: {Email}, TraceId: {TraceId}", model.Email, HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Passwords do not match." });
		}

		var user = await _userManager.FindByEmailAsync(model.Email);
		if (user == null)
		{
			_logger.LogWarning("Password reset failed for non-existing email. Email: {Email}, TraceId: {TraceId}", model.Email, HttpContext.TraceIdentifier);
			return BadRequest(new { message = "Invalid reset request." });
		}

		var result = await _userManager.ResetPasswordAsync(
			user,
			Uri.UnescapeDataString(model.Token),
			model.Password);

		if (!result.Succeeded)
		{
			_logger.LogWarning(
				"Password reset failed by Identity validation. UserId: {UserId}, Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
				user.Id,
				user.Email,
				result.Errors.Select(e => e.Description),
				HttpContext.TraceIdentifier);

			return BadRequest(new
			{
				message = "Password reset failed.",
				errors = result.Errors.Select(e => e.Description)
			});
		}

		_logger.LogInformation("Password reset successful. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);

		return Ok(new { message = "Password reset successful." });
	}

	[Authorize]
	[HttpPost("change-password")]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
			string.IsNullOrWhiteSpace(model.NewPassword) ||
			string.IsNullOrWhiteSpace(model.ConfirmPassword))
		{
			return BadRequest(new { message = "Current password, new password and confirm password are required." });
		}

		if (model.NewPassword != model.ConfirmPassword)
			return BadRequest(new { message = "Passwords do not match." });

		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Unauthorized(new { message = "User not authenticated." });

		if (user.IsBlocked)
		{
			_logger.LogWarning("Blocked user password change attempt. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);
			return Unauthorized(new { message = "Your account is blocked." });
		}

		var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
		if (!result.Succeeded)
		{
			_logger.LogWarning(
				"Password change failed. UserId: {UserId}, Email: {Email}, Errors: {Errors}, TraceId: {TraceId}",
				user.Id,
				user.Email,
				result.Errors.Select(e => e.Description),
				HttpContext.TraceIdentifier);

			return BadRequest(new
			{
				message = "Password change failed.",
				errors = result.Errors.Select(e => e.Description)
			});
		}

		_logger.LogInformation("Password changed successfully. UserId: {UserId}, Email: {Email}, TraceId: {TraceId}", user.Id, user.Email, HttpContext.TraceIdentifier);

		return Ok(new { message = "Password changed successfully." });
	}
}