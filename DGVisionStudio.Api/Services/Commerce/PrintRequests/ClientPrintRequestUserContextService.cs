using System.Security.Claims;

namespace DGVisionStudio.Api.Services;

public sealed class ClientPrintRequestUserContextService
{
    public string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
