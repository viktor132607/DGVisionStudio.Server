using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly string[] _protectedAdmins;

    public AdminUserService(
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

    public async Task<ControllerServiceResult> GetUsersAsync(PagedQueryDto query)
    {
        var source = _userManager.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Email);

        var total = await source.CountAsync();
        var users = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var items = new List<object>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new
            {
                user.Id,
                user.Email,
                user.IsBlocked,
                user.IsSeenByAdmin,
                user.CreatedAtUtc,
                user.EmailConfirmed,
                Roles = roles,
                IsProtectedAdmin = IsProtected(user)
            });
        }

        return ControllerServiceResult.Ok(new PagedResultDto<object>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            Items = items
        });
    }

    public async Task<ControllerServiceResult> MarkAllSeenAsync()
    {
        var users = await _userManager.Users
            .Where(x => !x.IsSeenByAdmin && !x.IsBlocked)
            .ToListAsync();

        foreach (var user in users)
        {
            user.IsSeenByAdmin = true;
            await _userManager.UpdateAsync(user);
        }

        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> GetUserAlbumsAsync(string id)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
            return ControllerServiceResult.NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var accesses = await _db.UserAlbumAccesses
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(x => x.AllowClientAccess && !x.IsUserUploaded && !assignedGalleryIds.Contains(x.Id))
            .OrderBy(x => x.Title)
            .Select(x => new
            {
                id = x.Id,
                title = x.Title,
                coverImageUrl = x.CoverImageUrl
            })
            .ToListAsync();

        return ControllerServiceResult.Ok(new
        {
            user = new
            {
                user.Id,
                user.Email,
                user.EmailConfirmed,
                user.IsBlocked,
                user.IsSeenByAdmin,
                user.CreatedAtUtc,
                Roles = roles
            },
            accesses,
            availableGalleries
        });
    }

    public async Task<ControllerServiceResult> MakeAdminAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
            await _userManager.AddToRoleAsync(user, "Admin");

        if (!await _userManager.IsInRoleAsync(user, "User"))
            await _userManager.AddToRoleAsync(user, "User");

        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> RemoveAdminAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();
        if (IsProtected(user))
            return ControllerServiceResult.BadRequest("Protected admin account.");

        await _userManager.RemoveFromRoleAsync(user, "Admin");
        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> BlockUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();
        if (IsProtected(user))
            return ControllerServiceResult.BadRequest("Protected admin account.");

        user.IsBlocked = true;
        user.IsSeenByAdmin = true;
        await _userManager.UpdateAsync(user);
        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> UnblockUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        user.IsBlocked = false;
        await _userManager.UpdateAsync(user);
        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> DeleteUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();
        if (IsProtected(user))
            return ControllerServiceResult.BadRequest("Protected admin account.");

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? ControllerServiceResult.NoContent()
            : ControllerServiceResult.BadRequest(result.Errors);
    }

    private bool IsProtected(ApplicationUser user) =>
        _protectedAdmins.Contains(user.Email?.ToLowerInvariant() ?? string.Empty);
}
