using System.Security.Claims;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.TestSupport;

internal static class TestDbContextFactory
{
    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

internal static class TestUsers
{
    public static ApplicationUser Create(string email = "user@example.com", string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Email = email,
        UserName = email
    };

    public static ClaimsPrincipal CreatePrincipal(ApplicationUser? user, bool isAdmin = false)
    {
        if (user == null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.Email ?? string.Empty)
        };

        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}

internal sealed class TestUserManager : UserManager<ApplicationUser>
{
    private readonly ApplicationUser? _user;
    private readonly HashSet<string> _roles;

    public TestUserManager(ApplicationUser? user, IEnumerable<string>? roles = null)
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
        _user = user;
        _roles = roles?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        return Task.FromResult(_user);
    }

    public override Task<ApplicationUser?> FindByIdAsync(string userId)
    {
        return Task.FromResult(_user != null && _user.Id == userId ? _user : null);
    }

    public override Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        return Task.FromResult(
            _user != null && string.Equals(_user.Email, email, StringComparison.OrdinalIgnoreCase)
                ? _user
                : null);
    }

    public override Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        return Task.FromResult<IList<string>>(_roles.ToList());
    }

    public override Task<bool> IsInRoleAsync(ApplicationUser user, string role)
    {
        return Task.FromResult(_roles.Contains(role));
    }

    public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        _roles.Add(role);
        return Task.FromResult(IdentityResult.Success);
    }

    public override Task<IdentityResult> RemoveFromRoleAsync(ApplicationUser user, string role)
    {
        _roles.Remove(role);
        return Task.FromResult(IdentityResult.Success);
    }

    public override Task<IdentityResult> UpdateAsync(ApplicationUser user)
    {
        return Task.FromResult(IdentityResult.Success);
    }

    public override Task<IdentityResult> DeleteAsync(ApplicationUser user)
    {
        return Task.FromResult(IdentityResult.Success);
    }
}

internal sealed class TestUserStore : IUserStore<ApplicationUser>
{
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    public void Dispose() { }
    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
}
