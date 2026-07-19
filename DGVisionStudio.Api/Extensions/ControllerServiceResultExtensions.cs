using System.Security.Claims;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Extensions;

public static class ControllerServiceResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, ControllerServiceResult result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => result.Value is null
                ? controller.Ok()
                : controller.Ok(result.Value),
            StatusCodes.Status204NoContent => controller.NoContent(),
            StatusCodes.Status400BadRequest => controller.BadRequest(result.Value),
            StatusCodes.Status401Unauthorized => controller.Unauthorized(result.Value),
            StatusCodes.Status403Forbidden => controller.Forbid(),
            StatusCodes.Status404NotFound => result.Value is null
                ? controller.NotFound()
                : controller.NotFound(result.Value),
            _ => controller.StatusCode(result.StatusCode, result.Value)
        };

    public static AdminRequestContext CreateAdminRequestContext(this ControllerBase controller)
    {
        var httpContext = controller.ControllerContext.HttpContext;
        var user = httpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

        return new AdminRequestContext(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            user.FindFirstValue(ClaimTypes.Email)
                ?? user.Identity?.Name
                ?? string.Empty,
            user.Identity?.Name ?? string.Empty,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty,
            httpContext?.TraceIdentifier ?? string.Empty);
    }
}
