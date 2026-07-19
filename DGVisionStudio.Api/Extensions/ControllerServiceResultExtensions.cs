using System.Security.Claims;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Extensions;

public static class ControllerServiceResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, ControllerServiceResult result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => controller.Ok(result.Value),
            StatusCodes.Status204NoContent => controller.NoContent(),
            StatusCodes.Status400BadRequest => controller.BadRequest(result.Value),
            StatusCodes.Status401Unauthorized => controller.Unauthorized(result.Value),
            StatusCodes.Status404NotFound => controller.NotFound(result.Value),
            _ => controller.StatusCode(result.StatusCode, result.Value)
        };

    public static AdminRequestContext CreateAdminRequestContext(this ControllerBase controller) =>
        new(
            controller.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            controller.User.FindFirstValue(ClaimTypes.Email)
                ?? controller.User.Identity?.Name
                ?? string.Empty,
            controller.User.Identity?.Name ?? string.Empty,
            controller.HttpContext.Connection.RemoteIpAddress?.ToString(),
            controller.Request.Headers.UserAgent.ToString(),
            controller.HttpContext.TraceIdentifier);
}
