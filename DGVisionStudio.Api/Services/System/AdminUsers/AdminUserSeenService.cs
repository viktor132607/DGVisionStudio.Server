using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserSeenService(
    UserManager<ApplicationUser> userManager)
{
    public async Task<ControllerServiceResult> MarkAllSeenAsync()
    {
        var users = await userManager.Users
            .Where(x => !x.IsSeenByAdmin && !x.IsBlocked)
            .ToListAsync();

        foreach (var user in users)
        {
            user.IsSeenByAdmin = true;
            await userManager.UpdateAsync(user);
        }

        return ControllerServiceResult.NoContent();
    }
}
