using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Auth;

public sealed class AdminUserRefactorTests
{
    [Fact]
    public void ProtectedPolicy_UsesConfiguredEmailsCaseInsensitively()
    {
        var policy = new AdminUserProtectedAccountPolicy(
            TestConfiguration.Create(
                ("Seed:PrimaryAdminEmail", "Protected@Example.com"),
                ("Seed:SecondaryAdminEmail", "second@example.com")));

        policy.IsProtected(
            TestUsers.Create("protected@example.com"))
            .Should().BeTrue();
        policy.IsProtected(
            TestUsers.Create("normal@example.com"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SeenService_MarksOnlyUnblockedUnseenUsers()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var unseen = TestUsers.Create("unseen@example.com", "unseen");
        var blocked = TestUsers.Create("blocked@example.com", "blocked");
        blocked.IsBlocked = true;
        unseen.IsSeenByAdmin = false;
        blocked.IsSeenByAdmin = false;

        fixture.Context.Users.AddRange(unseen, blocked);
        await fixture.Context.SaveChangesAsync();

        var manager = new ConfigurableUserManager([unseen, blocked])
        {
            UsersSource = fixture.Context.Users
        };
        var service = new AdminUserSeenService(manager);

        var result = await service.MarkAllSeenAsync();

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        unseen.IsSeenByAdmin.Should().BeTrue();
        blocked.IsSeenByAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task RoleService_AddsRequiredRolesAndProtectsRemoval()
    {
        var protectedUser = TestUsers.Create(
            "protected@example.com",
            "protected");
        var normal = TestUsers.Create(
            "normal@example.com",
            "normal");
        var manager = new ConfigurableUserManager(
            [protectedUser, normal]);
        var policy = new AdminUserProtectedAccountPolicy(
            TestConfiguration.Create(
                ("Seed:PrimaryAdminEmail", protectedUser.Email)));
        var service = new AdminUserRoleService(manager, policy);

        var madeAdmin = await service.MakeAdminAsync(normal.Id);
        var protectedRemoval =
            await service.RemoveAdminAsync(protectedUser.Id);

        madeAdmin.StatusCode.Should().Be(StatusCodes.Status200OK);
        manager.AddedRoles.Should().Contain((normal.Id, "Admin"));
        manager.AddedRoles.Should().Contain((normal.Id, "User"));
        protectedRemoval.StatusCode.Should().Be(
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AccountService_BlocksUnblocksAndReturnsIdentityDeleteErrors()
    {
        var user = TestUsers.Create("user@example.com", "user");
        var manager = new ConfigurableUserManager([user]);
        var policy = new AdminUserProtectedAccountPolicy(
            TestConfiguration.Create());
        var service = new AdminUserAccountService(manager, policy);

        (await service.BlockUserAsync(user.Id))
            .StatusCode.Should().Be(StatusCodes.Status200OK);
        user.IsBlocked.Should().BeTrue();
        user.IsSeenByAdmin.Should().BeTrue();

        (await service.UnblockUserAsync(user.Id))
            .StatusCode.Should().Be(StatusCodes.Status200OK);
        user.IsBlocked.Should().BeFalse();

        manager.DeleteResult = IdentityResult.Failed(
            new IdentityError { Code = "DeleteFailed" });

        (await service.DeleteUserAsync(user.Id))
            .StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task QueryService_ReturnsPagedUsersAndMissingUserAlbumsNotFound()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create(
            "query@example.com",
            "query-user");
        user.CreatedAtUtc = DateTime.UtcNow;

        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var manager = new ConfigurableUserManager([user])
        {
            UsersSource = fixture.Context.Users
        };
        manager.SetRoles(user, "User");

        var service = new AdminUserQueryService(
            manager,
            fixture.Context,
            new AdminUserProtectedAccountPolicy(
                TestConfiguration.Create()));

        var users = await service.GetUsersAsync(
            new PagedQueryDto
            {
                Page = 1,
                PageSize = 10
            });
        var missing = await service.GetUserAlbumsAsync("missing");

        users.StatusCode.Should().Be(StatusCodes.Status200OK);
        var page = users.Value
            .Should()
            .BeOfType<PagedResultDto<object>>()
            .Subject;
        page.Total.Should().Be(1);
        page.Items.Should().ContainSingle();

        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
