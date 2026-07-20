using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Coverage;

public sealed class AdminUserCoverageTests
{
    [Fact]
    public async Task GetUsersAndMarkAllSeen_HandlePagingRolesProtectionAndBlockedUsers()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var protectedUser = TestUsers.Create("protected@example.com", "protected");
        protectedUser.CreatedAtUtc = DateTime.UtcNow;
        protectedUser.IsSeenByAdmin = false;
        var blocked = TestUsers.Create("blocked@example.com", "blocked");
        blocked.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        blocked.IsSeenByAdmin = false;
        blocked.IsBlocked = true;
        fixture.Context.Users.AddRange(protectedUser, blocked);
        await fixture.Context.SaveChangesAsync();
        var manager = new ConfigurableUserManager([protectedUser, blocked])
        {
            UsersSource = fixture.Context.Users
        };
        manager.SetRoles(protectedUser, "Admin", "User");
        var service = new AdminUserService(
            manager,
            fixture.Context,
            TestConfiguration.Create(("Seed:PrimaryAdminEmail", "protected@example.com")));

        var users = await service.GetUsersAsync(new PagedQueryDto { Page = 1, PageSize = 1 });
        var marked = await service.MarkAllSeenAsync();

        users.StatusCode.Should().Be(StatusCodes.Status200OK);
        var page = users.Value.Should().BeOfType<PagedResultDto<object>>().Subject;
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();
        marked.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        protectedUser.IsSeenByAdmin.Should().BeTrue();
        blocked.IsSeenByAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserAlbumsAsync_ReturnsAssignedAndAvailableGalleries()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("client@example.com", "client");
        var category = new PortfolioCategory { Key = "client", Name = "Client", NameEn = "Client" };
        var assigned = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "assigned",
            Title = "Assigned",
            AllowClientAccess = true,
            IsUserUploaded = false
        };
        var available = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "available",
            Title = "Available",
            AllowClientAccess = true,
            IsUserUploaded = false
        };
        fixture.Context.AddRange(user, category, assigned, available);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserAlbumAccesses.Add(new UserAlbumAccess
        {
            UserId = user.Id,
            PortfolioAlbumId = assigned.Id,
            PreviewEnabled = true,
            DownloadEnabled = false
        });
        await fixture.Context.SaveChangesAsync();
        var manager = new ConfigurableUserManager([user]) { UsersSource = fixture.Context.Users };
        manager.SetRoles(user, "User");
        var service = new AdminUserService(manager, fixture.Context, TestConfiguration.Create());

        var result = await service.GetUserAlbumsAsync(user.Id);
        var missing = await service.GetUserAlbumsAsync("missing");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var value = result.Value!;
        var accesses = (global::System.Collections.IEnumerable)value.GetType()
            .GetProperty("accesses")!.GetValue(value)!;
        var availableGalleries = (global::System.Collections.IEnumerable)value.GetType()
            .GetProperty("availableGalleries")!.GetValue(value)!;
        accesses.Cast<object>().Should().ContainSingle();
        availableGalleries.Cast<object>().Should().ContainSingle();
    }

    [Fact]
    public async Task RoleBlockUnblockAndDeleteCommands_CoverSuccessProtectionAndIdentityFailure()
    {
        var protectedUser = TestUsers.Create("protected@example.com", "protected");
        var normal = TestUsers.Create("normal@example.com", "normal");
        var failing = TestUsers.Create("failing@example.com", "failing");
        var manager = new ConfigurableUserManager([protectedUser, normal, failing]);
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminUserService(
            manager,
            context,
            TestConfiguration.Create(("Seed:PrimaryAdminEmail", protectedUser.Email)));

        var madeAdmin = await service.MakeAdminAsync(normal.Id);
        var removed = await service.RemoveAdminAsync(normal.Id);
        var protectedRemoval = await service.RemoveAdminAsync(protectedUser.Id);
        var blocked = await service.BlockUserAsync(normal.Id);
        var unblocked = await service.UnblockUserAsync(normal.Id);
        var missing = await service.BlockUserAsync("missing");
        manager.DeleteResult = IdentityResult.Failed(new IdentityError { Code = "DeleteFailed" });
        var failedDelete = await service.DeleteUserAsync(failing.Id);
        manager.DeleteResult = IdentityResult.Success;
        var deleted = await service.DeleteUserAsync(normal.Id);

        madeAdmin.StatusCode.Should().Be(StatusCodes.Status200OK);
        manager.AddedRoles.Should().Contain((normal.Id, "Admin"));
        manager.AddedRoles.Should().Contain((normal.Id, "User"));
        removed.StatusCode.Should().Be(StatusCodes.Status200OK);
        manager.RemovedRoles.Should().Contain((normal.Id, "Admin"));
        protectedRemoval.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        blocked.StatusCode.Should().Be(StatusCodes.Status200OK);
        unblocked.StatusCode.Should().Be(StatusCodes.Status200OK);
        normal.IsBlocked.Should().BeFalse();
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        failedDelete.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }
}
