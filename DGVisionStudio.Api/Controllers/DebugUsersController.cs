using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/debug/users")]
public class DebugUsersController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;

	public DebugUsersController(UserManager<ApplicationUser> userManager)
	{
		_userManager = userManager;
	}

	[HttpGet]
	public async Task<IActionResult> GetUsers()
	{
		var users = _userManager.Users.ToList();

		var result = new List<object>();

		foreach (var user in users)
		{
			var roles = await _userManager.GetRolesAsync(user);

			result.Add(new
			{
				user.Id,
				user.Email,

				// Confirm email логиката е временно спряна,
				// но полето още го пазим за debug/съвместимост.
				user.EmailConfirmed,

				user.IsBlocked,
				Roles = roles
			});
		}

		return Ok(result);
	}
}