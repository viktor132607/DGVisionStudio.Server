using DGVisionStudio.Application.DTOs.Account;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly SignInManager<ApplicationUser> _signInManager;

	public AccountController(
		UserManager<ApplicationUser> userManager,
		SignInManager<ApplicationUser> signInManager)
	{
		_userManager = userManager;
		_signInManager = signInManager;
	}

	[HttpDelete("delete")]
	public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Password))
			return BadRequest(new { message = "Password is required." });

		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Unauthorized(new { message = "User not authenticated." });

		var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
		if (!passwordValid)
			return BadRequest(new { message = "Invalid password." });

		await _signInManager.SignOutAsync();

		var result = await _userManager.DeleteAsync(user);
		if (!result.Succeeded)
		{
			return BadRequest(new
			{
				message = "Account deletion failed.",
				errors = result.Errors.Select(x => x.Description)
			});
		}

		return Ok(new { message = "Account deleted successfully." });
	}
}