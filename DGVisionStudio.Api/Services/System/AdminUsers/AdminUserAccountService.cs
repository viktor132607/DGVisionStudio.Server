using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserAccountService(
    UserManager<ApplicationUser> userManager,
    AdminUserProtectedAccountPolicy protectedAccountPolicy)
{
    public async Task<ControllerServiceResult> BlockUserAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        if (protectedAccountPolicy.IsProtected(user))
            return ControllerServiceResult.BadRequest("Protected admin account.");

        user.IsBlocked = true;
        user.IsSeenByAdmin = true;
        await userManager.UpdateAsync(user);

        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> UnblockUserAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        user.IsBlocked = false;
        await userManager.UpdateAsync(user);

        return ControllerServiceResult.Ok();
    }

    public async Task<ControllerServiceResult> DeleteUserAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return ControllerServiceResult.NotFound();

        if (protectedAccountPolicy.IsProtected(user))
            return ControllerServiceResult.BadRequest("Protected admin account.");

        var result = await userManager.DeleteAsync(user);

        return result.Succeeded
            ? ControllerServiceResult.NoContent()
            : ControllerServiceResult.BadRequest(result.Errors);
    }
}
