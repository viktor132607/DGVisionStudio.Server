using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly AppDbContext _db;
	private readonly string[] _protectedAdmins;

	public AdminUsersController(
		UserManager<ApplicationUser> userManager,
		AppDbContext db,
		IConfiguration configuration)
	{
		_userManager = userManager;
		_db = db;
		_protectedAdmins = new[]
		{
			configuration["Seed:PrimaryAdminEmail"] ?? "dgvisionstudio@gmail.com",
			configuration["Seed:SecondaryAdminEmail"] ?? "iliev132607@gmail.com"
		}
		.Where(x => !string.IsNullOrWhiteSpace(x))
		.Select(x => x!.ToLowerInvariant())
		.ToArray();
	}

	[HttpGet]
	public async Task<IActionResult> GetUsers()
	{
		var users = _userManager.Users.ToList();
		var result = new List<object>(users.Count);

		foreach (var user in users.OrderByDescending(x => x.Email))
		{
			var roles = await _userManager.GetRolesAsync(user);

			result.Add(new
			{
				user.Id,
				user.Email,
				user.IsBlocked,

				// Confirm email логиката е временно спряна,
				// но полето още го връщаме за съвместимост.
				user.EmailConfirmed,

				Roles = roles,
				IsProtectedAdmin = _protectedAdmins.Contains(user.Email?.ToLowerInvariant() ?? string.Empty)
			});
		}

		return Ok(result);
	}

	[HttpGet("{id}/albums")]
	public async Task<IActionResult> GetUserAlbums(string id)
	{
		var user = await _userManager.FindByIdAsync(id);
		if (user == null) return NotFound();

		var roles = await _userManager.GetRolesAsync(user);

		var accesses = await _db.UserAlbumAccesses
			.Include(x => x.PortfolioAlbum)
			.Where(x => x.UserId == id)
			.OrderBy(x => x.PortfolioAlbum.Title)
			.Select(x => new
			{
				galleryId = x.PortfolioAlbumId,
				galleryTitle = x.PortfolioAlbum.Title,
				galleryDescription = x.PortfolioAlbum.Description,
				galleryCoverImageUrl = x.PortfolioAlbum.CoverImageUrl,
				previewEnabled = x.PreviewEnabled,
				downloadEnabled = x.DownloadEnabled,
				downloadExpiresAtUtc = x.DownloadExpiresAtUtc
			})
			.ToListAsync();

		var assignedGalleryIds = accesses.Select(x => x.galleryId).ToHashSet();

		var availableGalleries = await _db.PortfolioAlbums
			.Where(x => x.AllowClientAccess && !assignedGalleryIds.Contains(x.Id))
			.OrderBy(x => x.Title)
			.Select(x => new
			{
				id = x.Id,
				title = x.Title,
				coverImageUrl = x.CoverImageUrl
			})
			.ToListAsync();

		return Ok(new
		{
			user = new
			{
				user.Id,
				user.Email,

				// Confirm email логиката е временно спряна,
				// но полето още го връщаме за съвместимост.
				user.EmailConfirmed,

				user.IsBlocked,
				Roles = roles
			},
			accesses,
			availableGalleries
		});
	}

	[HttpPost("{id}/make-admin")]
	public async Task<IActionResult> MakeAdmin(string id)
	{
		var user = await _userManager.FindByIdAsync(id);
		if (user == null) return NotFound();

		if (!await _userManager.IsInRoleAsync(user, "Admin"))
			await _userManager.AddToRoleAsync(user, "Admin");

		if (!await _userManager.IsInRoleAsync(user, "User"))
			await _userManager.AddToRoleAsync(user, "User");

		return Ok();
	}

	[HttpPost("{id}/remove-admin")]
	public async Task<IActionResult> RemoveAdmin(string id)
	{
		var user = await _userManager.FindByIdAsync(id);
		if (user == null) return NotFound();

		if (_protectedAdmins.Contains(user.Email?.ToLowerInvariant() ?? string.Empty))
			return BadRequest("Protected admin account.");

		await _userManager.RemoveFromRoleAsync(user, "Admin");
		return Ok();
	}

	[HttpPost("{id}/block")]
	public async Task<IActionResult> BlockUser(string id)
	{
		var user = await _userManager.FindByIdAsync(id);
		if (user == null) return NotFound();

		if (_protectedAdmins.Contains(user.Email?.ToLowerInvariant() ?? string.Empty))
			return BadRequest("Protected admin account.");

		user.IsBlocked = true;
		await _userManager.UpdateAsync(user);
		return Ok();
	}

	[HttpPost("{id}/unblock")]
	public async Task<IActionResult> UnblockUser(string id)
	{
		var user = await _userManager.FindByIdAsync(id);
		if (user == null) return NotFound();

		user.IsBlocked = false;
		await _userManager.UpdateAsync(user);
		return Ok();
	}

	[HttpDelete("{id}")]
	public async Task<IActionResult> DeleteUser(string id)
	{
		var user = await _userManager.FindByIdAsync(id);
		if (user == null) return NotFound();

		if (_protectedAdmins.Contains(user.Email?.ToLowerInvariant() ?? string.Empty))
			return BadRequest("Protected admin account.");

		var result = await _userManager.DeleteAsync(user);
		return result.Succeeded ? NoContent() : BadRequest(result.Errors);
	}
}