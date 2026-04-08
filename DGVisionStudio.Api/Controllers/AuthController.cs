using System.Net;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly SignInManager<ApplicationUser> _signInManager;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;

	public AuthController(
		UserManager<ApplicationUser> userManager,
		SignInManager<ApplicationUser> signInManager,
		IEmailService emailService,
		IConfiguration configuration)
	{
		_userManager = userManager;
		_signInManager = signInManager;
		_emailService = emailService;
		_configuration = configuration;
	}

	[HttpPost("register")]
	public async Task<IActionResult> Register([FromBody] RegisterRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email) ||
			string.IsNullOrWhiteSpace(model.Password) ||
			string.IsNullOrWhiteSpace(model.ConfirmPassword))
		{
			return BadRequest(new { message = "Email, password and confirm password are required." });
		}

		if (model.Password != model.ConfirmPassword)
			return BadRequest(new { message = "Passwords do not match." });

		var existingUser = await _userManager.FindByEmailAsync(model.Email);
		if (existingUser != null)
			return BadRequest(new { message = "User with this email already exists." });

		var normalizedEmail = model.Email.Trim();

		var user = new ApplicationUser
		{
			UserName = normalizedEmail,
			Email = normalizedEmail,

			// Confirm email логиката е временно спряна.
			// EmailConfirmed = false
			EmailConfirmed = true
		};

		var result = await _userManager.CreateAsync(user, model.Password);
		if (!result.Succeeded)
		{
			return BadRequest(new
			{
				message = "Registration failed.",
				errors = result.Errors.Select(e => e.Description)
			});
		}

		await _userManager.AddToRoleAsync(user, "User");

		// Потвърждение по имейл е временно спряно.
		/*
		try
		{
			var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
			var apiUrl = (_configuration["Api:Url"] ?? "http://localhost:10000").Replace("https://", "http://").TrimEnd('/');

			var confirmUrl =
				$"{apiUrl}/api/auth/confirm-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

			var safeConfirmUrl = WebUtility.HtmlEncode(confirmUrl);

			await _emailService.SendAsync(
				user.Email!,
				"Confirm your account",
				$"""
				<div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#111;">
					<p>Please confirm your email.</p>
					<p><a href="{safeConfirmUrl}">Confirm email</a></p>
				</div>
				""");
		}
		catch
		{
			return Ok(new { message = "Registration successful, but confirmation email could not be sent." });
		}

		return Ok(new { message = "Registration successful. Please check your email to confirm your account." });
		*/

		return Ok(new { message = "Registration successful." });
	}

	[HttpGet("check-email")]
	public async Task<IActionResult> CheckEmail([FromQuery] string email)
	{
		if (string.IsNullOrWhiteSpace(email))
			return BadRequest(new { message = "Email is required." });

		var user = await _userManager.FindByEmailAsync(email);

		return Ok(new
		{
			exists = user != null
		});
	}

	[HttpGet("confirm-email")]
	public async Task<IActionResult> ConfirmEmail()
	{
		var email = Request.Query["email"].ToString();
		var token = Request.Query["token"].ToString();

		var frontendUrl = (_configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');

		// Confirm email логиката е временно спряна.
		/*
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
			return Redirect($"{frontendUrl}/identity/login?confirmed=0");

		var user = await _userManager.FindByEmailAsync(email);
		if (user == null)
			return Redirect($"{frontendUrl}/identity/login?confirmed=0");

		var decodedToken = Uri.UnescapeDataString(token);

		var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

		return result.Succeeded
			? Redirect($"{frontendUrl}/identity/login?confirmed=1")
			: Redirect($"{frontendUrl}/identity/login?confirmed=0");
		*/

		await Task.CompletedTask;
		return Redirect($"{frontendUrl}/identity/login");
	}

	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
			return BadRequest(new { message = "Email and password are required." });

		var user = await _userManager.FindByEmailAsync(model.Email);
		if (user == null)
			return Unauthorized(new { message = "Invalid email or password." });

		if (user.IsBlocked)
			return Unauthorized(new { message = "Your account is blocked." });

		// Confirm email логиката е временно спряна.
		/*
		if (!user.EmailConfirmed)
			return Unauthorized(new { message = "Please confirm your email before logging in." });
		*/

		var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, false, false);
		if (!result.Succeeded)
			return Unauthorized(new { message = "Invalid email or password." });

		var roles = await _userManager.GetRolesAsync(user);

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
		await _signInManager.SignOutAsync();
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

	[HttpPost("forgot-password")]
	public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
	{
		if (string.IsNullOrWhiteSpace(model.Email))
			return BadRequest(new { message = "Email is required." });

		var user = await _userManager.FindByEmailAsync(model.Email);

		// Confirm email логиката е временно спряна.
		/*
		if (user == null || !user.EmailConfirmed)
			return Ok(new { message = "If an account with that email exists, a reset link has been sent." });
		*/
		if (user == null)
			return Ok(new { message = "If an account with that email exists, a reset link has been sent." });

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
			return BadRequest(new { message = "Invalid reset request." });
		}

		if (model.Password != model.ConfirmPassword)
			return BadRequest(new { message = "Passwords do not match." });

		var user = await _userManager.FindByEmailAsync(model.Email);
		if (user == null)
			return BadRequest(new { message = "Invalid reset request." });

		var result = await _userManager.ResetPasswordAsync(
			user,
			Uri.UnescapeDataString(model.Token),
			model.Password);

		if (!result.Succeeded)
		{
			return BadRequest(new
			{
				message = "Password reset failed.",
				errors = result.Errors.Select(e => e.Description)
			});
		}

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
			return Unauthorized(new { message = "Your account is blocked." });

		var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
		if (!result.Succeeded)
		{
			return BadRequest(new
			{
				message = "Password change failed.",
				errors = result.Errors.Select(e => e.Description)
			});
		}

		return Ok(new { message = "Password changed successfully." });
	}
}