using System.Security.Claims;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DGVisionStudio.Tests.TestSupport;

internal sealed class ConfigurableUserManager : UserManager<ApplicationUser>
{
    private readonly List<ApplicationUser> _users;
    private readonly Dictionary<string, HashSet<string>> _roles = new(StringComparer.OrdinalIgnoreCase);

    public ConfigurableUserManager(IEnumerable<ApplicationUser>? users = null)
        : base(
            new TestUserStore(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        _users = users?.ToList() ?? [];
    }

    public ApplicationUser? CurrentUser { get; set; }
    public IQueryable<ApplicationUser>? UsersSource { get; set; }
    public bool IsLockedOutResult { get; set; }
    public bool CheckPasswordResult { get; set; } = true;
    public IdentityResult CreateResult { get; set; } = IdentityResult.Success;
    public IdentityResult UpdateResult { get; set; } = IdentityResult.Success;
    public IdentityResult DeleteResult { get; set; } = IdentityResult.Success;
    public IdentityResult ResetPasswordResult { get; set; } = IdentityResult.Success;
    public IdentityResult ChangePasswordResult { get; set; } = IdentityResult.Success;
    public string PasswordResetToken { get; set; } = "reset-token";
    public ApplicationUser? CreatedUser { get; private set; }
    public List<(string UserId, string Role)> AddedRoles { get; } = [];
    public List<(string UserId, string Role)> RemovedRoles { get; } = [];

    public override IQueryable<ApplicationUser> Users => UsersSource ?? _users.AsQueryable();

    public void SetRoles(ApplicationUser user, params string[] roles) =>
        _roles[user.Id] = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) =>
        Task.FromResult(CurrentUser);

    public override Task<ApplicationUser?> FindByIdAsync(string userId) =>
        Task.FromResult(_users.FirstOrDefault(x => x.Id == userId));

    public override Task<ApplicationUser?> FindByEmailAsync(string email) =>
        Task.FromResult(_users.FirstOrDefault(x =>
            string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)));

    public override Task<IList<string>> GetRolesAsync(ApplicationUser user) =>
        Task.FromResult<IList<string>>(
            _roles.TryGetValue(user.Id, out var roles) ? roles.ToList() : []);

    public override Task<bool> IsInRoleAsync(ApplicationUser user, string role) =>
        Task.FromResult(_roles.TryGetValue(user.Id, out var roles) && roles.Contains(role));

    public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        if (!_roles.TryGetValue(user.Id, out var roles))
            _roles[user.Id] = roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        roles.Add(role);
        AddedRoles.Add((user.Id, role));
        return Task.FromResult(IdentityResult.Success);
    }

    public override Task<IdentityResult> RemoveFromRoleAsync(ApplicationUser user, string role)
    {
        if (_roles.TryGetValue(user.Id, out var roles))
            roles.Remove(role);
        RemovedRoles.Add((user.Id, role));
        return Task.FromResult(IdentityResult.Success);
    }

    public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
    {
        CreatedUser = user;
        if (CreateResult.Succeeded)
            _users.Add(user);
        return Task.FromResult(CreateResult);
    }

    public override Task<IdentityResult> UpdateAsync(ApplicationUser user) =>
        Task.FromResult(UpdateResult);

    public override Task<IdentityResult> DeleteAsync(ApplicationUser user)
    {
        if (DeleteResult.Succeeded)
            _users.Remove(user);
        return Task.FromResult(DeleteResult);
    }

    public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password) =>
        Task.FromResult(CheckPasswordResult);

    public override Task<bool> IsLockedOutAsync(ApplicationUser user) =>
        Task.FromResult(IsLockedOutResult);

    public override Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user) =>
        Task.FromResult(PasswordResetToken);

    public override Task<IdentityResult> ResetPasswordAsync(
        ApplicationUser user,
        string token,
        string newPassword) => Task.FromResult(ResetPasswordResult);

    public override Task<IdentityResult> ChangePasswordAsync(
        ApplicationUser user,
        string currentPassword,
        string newPassword) => Task.FromResult(ChangePasswordResult);
}

internal sealed class ConfigurableSignInManager : SignInManager<ApplicationUser>
{
    public ConfigurableSignInManager(UserManager<ApplicationUser> userManager)
        : base(
            userManager,
            new HttpContextAccessor(),
            new TestClaimsPrincipalFactory(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<ApplicationUser>())
    {
    }

    public SignInResult PasswordSignInResult { get; set; } = SignInResult.Success;
    public bool SignedOut { get; private set; }
    public string? LastUserName { get; private set; }

    public override Task<SignInResult> PasswordSignInAsync(
        string userName,
        string password,
        bool isPersistent,
        bool lockoutOnFailure)
    {
        LastUserName = userName;
        return Task.FromResult(PasswordSignInResult);
    }

    public override Task SignOutAsync()
    {
        SignedOut = true;
        return Task.CompletedTask;
    }

    private sealed class TestClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user) =>
            Task.FromResult(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)],
                "TestIdentity")));
    }
}

internal sealed class RecordingEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Messages { get; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public Task SendAsync(string toEmail, string subject, string body)
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
        Messages.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed class StubPrivacyService : IPrivacyService
{
    public GdprExportResponse? ExportResult { get; set; }
    public bool AnonymizeResult { get; set; }
    public string? LastExportUserId { get; private set; }
    public string? LastAnonymizedUserId { get; private set; }

    public Task<GdprExportResponse?> ExportUserDataAsync(string userId)
    {
        LastExportUserId = userId;
        return Task.FromResult(ExportResult);
    }

    public Task<bool> AnonymizeUserDataAsync(string userId)
    {
        LastAnonymizedUserId = userId;
        return Task.FromResult(AnonymizeResult);
    }
}

internal static class TestConfiguration
{
    public static IConfiguration Create(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Key, x => x.Value))
            .Build();
}
