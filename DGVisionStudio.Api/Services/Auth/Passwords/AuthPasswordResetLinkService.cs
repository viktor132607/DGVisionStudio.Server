using System.Net;

namespace DGVisionStudio.Api.Services;

public sealed class AuthPasswordResetLinkService(IConfiguration configuration)
{
    public string BuildApiResetUrl(string email, string token)
    {
        var apiUrl = (configuration["Api:Url"] ?? "http://localhost:10000")
            .TrimEnd('/');

        return $"{apiUrl}/api/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    public string BuildSafeApiResetUrl(string email, string token) =>
        WebUtility.HtmlEncode(BuildApiResetUrl(email, token));

    public string GetFrontendRedirectUrl(string email, string token)
    {
        var frontendUrl =
            (configuration["Frontend:Url"] ?? "http://localhost:5173")
            .TrimEnd('/');

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(token))
        {
            return $"{frontendUrl}/identity/login";
        }

        return $"{frontendUrl}/identity/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }
}
