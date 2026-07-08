using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Tests.Auth;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task MakeAdmin_ReturnsNotFound_WhenUserDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateController(context, user: null);

        var result = await controller.MakeAdmin("missing-user");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RemoveAdmin_ReturnsBadRequest_WhenUserIsProtectedAdmin()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("dgvisionstudio@gmail.com");
        var controller = CreateController(context, user);

        var result = await controller.RemoveAdmin(user.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BlockUser_ReturnsBadRequest_WhenUserIsProtectedAdmin()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("dgvisionstudio@gmail.com");
        var controller = CreateController(context, user);

        var result = await controller.BlockUser(user.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        user.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task UnblockUser_ReturnsOk_AndClearsBlockedFlag()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("user@example.com");
        user.IsBlocked = true;
        var controller = CreateController(context, user);

        var result = await controller.UnblockUser(user.Id);

        result.Should().BeOfType<OkResult>();
        user.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_ReturnsBadRequest_WhenUserIsProtectedAdmin()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("dgvisionstudio@gmail.com");
        var controller = CreateController(context, user);

        var result = await controller.DeleteUser(user.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteUser_ReturnsNoContent_WhenDeleteSucceeds()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("user@example.com");
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
}
