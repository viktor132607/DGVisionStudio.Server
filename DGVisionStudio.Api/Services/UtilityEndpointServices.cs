using DGVisionStudio.Api.Middleware;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class CsrfTokenService : ICsrfTokenService
{
    public string GenerateToken() => CsrfProtectionMiddleware.GenerateToken();
}

public sealed class DebugUserService : IDebugUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DebugUserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ControllerServiceResult> GetUsersAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<object>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new
            {
                user.Id,
                user.Email,
                user.EmailConfirmed,
                user.IsBlocked,
                Roles = roles
            });
        }

        return ControllerServiceResult.Ok(result);
    }
}

public sealed class SiteSettingsService : ISiteSettingsService
{
    private readonly AppDbContext _context;

    public SiteSettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await _context.SiteSettings
            .OrderBy(x => x.Key)
            .Select(x => new { x.Key, x.Value, x.Description, x.UpdatedAtUtc })
            .ToListAsync());
}

public sealed class HomeStatusService : IHomeStatusService
{
    public ControllerServiceResult GetStatus() =>
        ControllerServiceResult.Ok(new { message = "DG Vision Studio API running" });
}
