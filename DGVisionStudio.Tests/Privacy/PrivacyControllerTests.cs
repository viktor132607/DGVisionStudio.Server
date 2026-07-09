using DGVisionStudio.Api.Controllers;
using DGVisionStudio.Api.Models;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Privacy;

public sealed class PrivacyControllerTests
{
    [Fact]
    public async Task Export_ReturnsUnauthorized_WhenUserIsMissing()
    {
        var controller = new PrivacyController(new TestUserManager(user: null), new StubPrivacyService());

        var result = await controller.Export();

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.Code.Should().Be(ApiErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Export_ReturnsUserData_WhenUserExists()
    {
        var user = TestUsers.Create("user@example.com", "user-1");
        var export = new GdprExportResponse(
            DateTime.UtcNow,
            new GdprAccountExport(user.Id, user.Email, user.UserName, null, DateTime.UtcNow, false),
            [],
            [],
            [],
            []);
        var controller = new PrivacyController(new TestUserManager(user), new StubPrivacyService(export));

        var result = await controller.Export();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(export);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsBadRequest_WhenNotConfirmed()
    {
        var user = TestUsers.Create("user@example.com", "user-1");
        var controller = new PrivacyController(new TestUserManager(user), new StubPrivacyService());

        var result = await controller.DeleteAccount(new DeleteAccountRequest { Confirm = false });

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.Code.Should().Be(ApiErrorCodes.ValidationError);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsNoContent_WhenConfirmed()
    {
        var user = TestUsers.Create("user@example.com", "user-1");
        var controller = new PrivacyController(new TestUserManager(user), new StubPrivacyService(anonymizeResult: true));

        var result = await controller.DeleteAccount(new DeleteAccountRequest { Confirm = true });

        result.Should().BeOfType<NoContentResult>();
    }

    private sealed class StubPrivacyService(
        GdprExportResponse? export = null,
        bool anonymizeResult = false) : IPrivacyService
    {
        public Task<GdprExportResponse?> ExportUserDataAsync(string userId)
        {
            return Task.FromResult(export);
        }

        public Task<bool> AnonymizeUserDataAsync(string userId)
        {
            return Task.FromResult(anonymizeResult);
        }
    }
}
