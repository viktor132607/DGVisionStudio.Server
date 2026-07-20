using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Tests.PrintRequests;

public sealed class AdminPrintRequestServiceTests
{
    [Fact]
    public async Task Facade_DelegatesQueryAndCommandOperations()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminPrintRequestService(
            new AdminPrintRequestQueryService(context),
            new AdminPrintRequestCommandService(context));

        var all = await service.GetAllAsync();
        var marked = await service.MarkAllSeenAsync();
        var missing = await service.GetByIdAsync(999);

        all.StatusCode.Should().Be(StatusCodes.Status200OK);
        marked.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public sealed class ClientPrintRequestEndpointServiceTests
{
    [Fact]
    public async Task Endpoints_RequireUserAndValidateAlbumBeforeCreatingRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new ClientPrintRequestEndpointService(context);
        var anonymous = TestUsers.CreatePrincipal(null);
        var user = TestUsers.Create("client@example.com", "user-1");
        var principal = TestUsers.CreatePrincipal(user);

        var unauthorized = await service.GetMineAsync(anonymous);
        var mine = await service.GetMineAsync(principal);
        var invalid = await service.CreateAsync(principal, new CreatePrintRequestDto());

        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        mine.StatusCode.Should().Be(StatusCodes.Status200OK);
        mine.Value.Should().BeOfType<List<PrintRequestDto>>().Which.Should().BeEmpty();
        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
