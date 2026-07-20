using System.Security.Claims;
using DGVisionStudio.Api.Controllers;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Controllers;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task DeleteAccount_DelegatesAuthenticatedPrincipalAndPassword()
    {
        ClaimsPrincipal? capturedPrincipal = null;
        string? capturedPassword = null;
        var service = new StubAccountEndpointService
        {
            Handler = (principal, password) =>
            {
                capturedPrincipal = principal;
                capturedPassword = password;
                return Task.FromResult(ControllerServiceResult.NoContent());
            }
        };
        var controller = new AccountController(service);
        ControllerTestContext.Attach(controller, "user-42");

        var result = await controller.DeleteAccount(
            new DGVisionStudio.Application.DTOs.Account.DeleteAccountRequest { Password = "secret" });

        result.Should().BeOfType<NoContentResult>();
        capturedPrincipal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("user-42");
        capturedPassword.Should().Be("secret");
    }
}

public sealed class AdminAuditLogsControllerTests
{
    [Fact]
    public async Task GetAuditLogs_ForwardsAllFiltersAndMapsResult()
    {
        (int Page, int Size, string? Type, string? Id, string? Email, string? Action) captured = default;
        var service = new StubAdminAuditLogQueryService
        {
            QueryHandler = (page, size, type, id, email, action) =>
            {
                captured = (page, size, type, id, email, action);
                return Task.FromResult(ControllerServiceResult.Ok(new[] { "audit" }));
            }
        };
        var controller = new AdminAuditLogsController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.GetAuditLogs(2, 25, "Album", "7", "admin@example.com", "Delete");

        result.Should().BeOfType<OkObjectResult>();
        captured.Should().Be((2, 25, "Album", "7", "admin@example.com", "Delete"));
    }
}

public sealed class AdminDashboardControllerTests
{
    [Fact]
    public async Task GetStats_MapsServiceResult()
    {
        var service = new StubAdminStatisticsService
        {
            DashboardHandler = () => Task.FromResult(ControllerServiceResult.Ok(new { Total = 9 }))
        };
        var controller = new AdminDashboardController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.GetStats();

        result.Should().BeOfType<OkObjectResult>();
    }
}

public sealed class AdminNotificationsControllerTests
{
    [Fact]
    public async Task GetCounts_MapsServiceResult()
    {
        var service = new StubAdminStatisticsService
        {
            NotificationsHandler = () => Task.FromResult(ControllerServiceResult.Ok(new { NewUsers = 2 }))
        };
        var controller = new AdminNotificationsController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.GetCounts();

        result.Should().BeOfType<OkObjectResult>();
    }
}

public sealed class CsrfControllerTests
{
    [Fact]
    public void GetToken_ReturnsTokenAndWritesSecureReadableCookie()
    {
        var controller = new CsrfController(new StubCsrfTokenService("token-123"));
        var context = ControllerTestContext.Attach(controller);

        var result = controller.GetToken();

        result.Should().BeOfType<OkObjectResult>();
        context.Response.Headers.SetCookie.ToString().Should()
            .Contain("DGVisionStudio.Csrf=token-123")
            .And.Contain("secure")
            .And.Contain("samesite=none");
    }
}

public sealed class DebugUsersControllerTests
{
    [Fact]
    public async Task GetUsers_MapsServiceResult()
    {
        var controller = new DebugUsersController(new StubDebugUserService
        {
            Result = ControllerServiceResult.Ok(new[] { "user" })
        });
        ControllerTestContext.Attach(controller);

        var result = await controller.GetUsers();

        result.Should().BeOfType<OkObjectResult>();
    }
}

public sealed class HomeControllerTests
{
    [Fact]
    public void Get_MapsServiceResult()
    {
        var controller = new HomeController(new StubHomeStatusService
        {
            Result = ControllerServiceResult.Ok(new { Status = "Running" })
        });
        ControllerTestContext.Attach(controller);

        var result = controller.Get();

        result.Should().BeOfType<OkObjectResult>();
    }
}

public sealed class SiteSettingsControllerTests
{
    [Fact]
    public async Task GetAll_MapsServiceResult()
    {
        var controller = new SiteSettingsController(new StubSiteSettingsService
        {
            Result = ControllerServiceResult.Ok(new Dictionary<string, string>())
        });
        ControllerTestContext.Attach(controller);

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>();
    }
}
