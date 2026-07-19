using DGVisionStudio.Api.Middleware;
using DGVisionStudio.Api.Services.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class CsrfTokenService : ICsrfTokenService
{
    public string GenerateToken() =>
        CsrfProtectionMiddleware.GenerateToken();
}
