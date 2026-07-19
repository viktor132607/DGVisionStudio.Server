using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _service;

    [ActivatorUtilitiesConstructor]
    public AdminUsersController(IAdminUserService service)
    {
        _service = service;
    }

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IConfiguration configuration)
        : this(new AdminUserService(userManager, db, configuration))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] PagedQueryDto query) =>
        this.ToActionResult(await _service.GetUsersAsync(query));

    [HttpPut("seen")]
    public async Task<IActionResult> MarkAllSeen() =>
        this.ToActionResult(await _service.MarkAllSeenAsync());

    [HttpGet("{id}/albums")]
    public async Task<IActionResult> GetUserAlbums(string id) =>
        this.ToActionResult(await _service.GetUserAlbumsAsync(id));

    [HttpPost("{id}/make-admin")]
    public async Task<IActionResult> MakeAdmin(string id) =>
        this.ToActionResult(await _service.MakeAdminAsync(id));

    [HttpPost("{id}/remove-admin")]
    public async Task<IActionResult> RemoveAdmin(string id) =>
        this.ToActionResult(await _service.RemoveAdminAsync(id));

    [HttpPost("{id}/block")]
    public async Task<IActionResult> BlockUser(string id) =>
        this.ToActionResult(await _service.BlockUserAsync(id));

    [HttpPost("{id}/unblock")]
    public async Task<IActionResult> UnblockUser(string id) =>
        this.ToActionResult(await _service.UnblockUserAsync(id));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id) =>
        this.ToActionResult(await _service.DeleteUserAsync(id));
}
