using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Auth;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task MakeAdmin_ReturnsNotFound_WhenUserDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, user: null);

        var result = await controller.MakeAdmin("missing-user");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RemoveAdmin_ReturnsBadRequest_WhenUserIsProtectedAdmin()
    {
        await using var context = CreateContext();
        var user = CreateUser("dgvisionstudio@gmail.com");
        var controller = CreateController(context, user);

        var result = await controller.RemoveAdmin(user.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BlockUser_ReturnsBadRequest_WhenUserIsProtectedAdmin()
    {
        await using var context = CreateContext();
        var user = CreateUser("dgvisionstudio@gmail.com");
        var controller = CreateController(context, user);

        var result = await controller.BlockUser(user.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        user.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task UnblockUser_ReturnsOk_AndClearsBlockedFlag()
    {
        await using var context = CreateContext();
        var user = CreateUser("user@example.com");
        user.IsBlocked = true;
        var controller = CreateController(context, user);

        var result = await controller.UnblockUser(user.Id);

        result.Should().BeOfType<OkResult>();
        user.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_ReturnsBadRequest_WhenUserIsProtectedAdmin()
    {
        await using var context = CreateContext();
        var user = CreateUser("dgvisionstudio@gmail.com");
        var controller = CreateController(context, user);

        var result = await controller.DeleteUser(user.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteUser_ReturnsNoContent_WhenDeleteSucceeds()
    {
        await using var context = CreateContext();
        var user = CreateUser("user@example.com");
        var controller = CreateController(context, user);

        var result = await controller.DeleteUser(user.Id);

        result.Should().BeOfType<NoContentResult>();
    }

    private static AdminUsersController CreateController(AppDbContext context, ApplicationUser? user)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:PrimaryAdminEmail"] = "dgvisionstudio@gmail.com",
                ["Seed:SecondaryAdminEmail"] = "iliev132607@gmail.com"
            })
            .Build();

        return new AdminUsersController(new TestUserManager(user), context, configuration);
    }

    private static ApplicationUser CreateUser(string email) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Email = email,
        UserName = email
    };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestUserManager : UserManager<ApplicationUser>
    {
        private readonly ApplicationUser? _user;

        public TestUserManager(ApplicationUser? user)
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
        }

        public override Task<ApplicationUser?> FindByIdAsync(string userId)
        {
            return Task.FromResult(_user != null && _user.Id == userId ? _user : null);
        }

        public override Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        {
            return Task.FromResult(false);
        }

        public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<IdentityResult> RemoveFromRoleAsync(ApplicationUser user, string role)
        {
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

    private sealed class TestUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
