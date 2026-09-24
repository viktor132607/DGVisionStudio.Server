using System.Security.Claims;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class ClientGalleryEndpointUserContextService(
    UserManager<ApplicationUser> userManager)
{
    public Task<ApplicationUser?> ResolveAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return userManager.GetUserAsync(principal);
    }

    public static ControllerServiceResult Unauthenticated() =>
        ControllerServiceResult.Unauthorized(
            new { message = "User not authenticated." });
}
