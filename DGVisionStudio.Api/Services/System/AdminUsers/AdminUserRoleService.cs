using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserRoleService(
    UserManager<ApplicationUser> userManager,
    AdminUserProtectedAccountPolicy protectedAccountPolicy)
{
    public async Task<ControllerServiceResult> MakeAdminAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        if (!await userManager.IsInRoleAsync(user, "Admin"))
            await userManager.AddToRoleAsync(user, "Admin");

        if (!await userManager.IsInRoleAsync(user, "User"))
            await userManager.AddToRoleAsync(user, "User");

        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> RemoveAdminAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        if (protectedAccountPolicy.IsProtected(user))
            return ControllerServiceResult.BadRequest("Protected admin account.");

        await userManager.RemoveFromRoleAsync(user, "Admin");
        return ControllerServiceResult.Ok();
    }
}
