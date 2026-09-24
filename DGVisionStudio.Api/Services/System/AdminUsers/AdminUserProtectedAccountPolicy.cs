using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserProtectedAccountPolicy
{
    private readonly HashSet<string> protectedEmails;

    public AdminUserProtectedAccountPolicy(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        protectedEmails = new HashSet<string>(
            new[]
            {
                configuration["Seed:PrimaryAdminEmail"] ?? "dgvisionstudio@gmail.com",
                configuration["Seed:SecondaryAdminEmail"] ?? "iliev132607@gmail.com"
            }
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsProtected(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return !string.IsNullOrWhiteSpace(user.Email) &&
            protectedEmails.Contains(user.Email);
    }
}
