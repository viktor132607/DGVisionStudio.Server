using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class DebugUserService(UserManager<ApplicationUser> userManager) : IDebugUserService
{
    public async Task<ControllerServiceResult> GetUsersAsync()
    {
        var users = await userManager.Users.ToListAsync();
        var result = new List<object>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
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
