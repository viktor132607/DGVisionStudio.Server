using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class PrivacyAnonymizationMapper
{
    public string AnonymizeAccount(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var anonymizedEmail =
            $"deleted-user-{Guid.NewGuid():N}@deleted.local";
        var normalizedAnonymizedEmail = anonymizedEmail.ToUpperInvariant();

        user.UserName = anonymizedEmail;
        user.NormalizedUserName = normalizedAnonymizedEmail;
        user.Email = anonymizedEmail;
        user.NormalizedEmail = normalizedAnonymizedEmail;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        user.TwoFactorEnabled = false;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.IsBlocked = true;
        user.IsSeenByAdmin = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        return anonymizedEmail;
    }

    public void AnonymizePrintRequest(
        PrintRequest request,
        string anonymizedEmail)
    {
        request.FullName = "Deleted user";
        request.Email = anonymizedEmail;
        request.Phone = null;
        request.Notes = null;
        request.UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AnonymizeContactRequest(
        ContactRequest request,
        string anonymizedEmail)
    {
        request.Name = "Deleted user";
        request.Email = anonymizedEmail;
        request.Phone = null;
        request.Subject = null;
        request.Message = "Deleted by GDPR request.";
        request.AdminComment = null;
        request.IsArchived = true;
        request.UpdatedAtUtc = DateTime.UtcNow;
    }
}
