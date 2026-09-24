using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserQueryService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    AdminUserProtectedAccountPolicy protectedAccountPolicy)
{
    public async Task<ControllerServiceResult> GetUsersAsync(PagedQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = userManager.Users
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
            var roles = await userManager.GetRolesAsync(user);
            items.Add(new
            {
                user.Id,
                user.Email,
                user.IsBlocked,
                user.IsSeenByAdmin,
                user.CreatedAtUtc,
                user.EmailConfirmed,
                Roles = roles,
                IsProtectedAdmin = protectedAccountPolicy.IsProtected(user)
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

    public async Task<ControllerServiceResult> GetUserAlbumsAsync(string id)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
            return ControllerServiceResult.NotFound();

        var roles = await userManager.GetRolesAsync(user);
        var accesses = await db.UserAlbumAccesses
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

        var assignedGalleryIds = accesses
            .Select(x => x.galleryId)
            .ToHashSet();

        var availableGalleries = await db.PortfolioAlbums
            .AsNoTracking()
            .Where(x =>
                x.AllowClientAccess &&
                !x.IsUserUploaded &&
                !assignedGalleryIds.Contains(x.Id))
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
}
