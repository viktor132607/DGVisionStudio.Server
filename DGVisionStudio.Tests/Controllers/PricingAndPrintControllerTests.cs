using System.Security.Claims;
using DGVisionStudio.Api.Controllers;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Controllers;

public sealed class AdminPricingControllerTests
{
    [Fact]
    public async Task Create_MapsPricingValidationExceptionToBadRequest()
    {
        var service = new StubPricingService
        {
            CreateHandler = _ => Task.FromException<PricingItemResponse>(
                new PricingValidationException("Title is required."))
        };
        var controller = new AdminPricingController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.Create(new PricingItemRequest());

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var controller = new AdminPricingController(new StubPricingService());
        ControllerTestContext.Attach(controller);

        var result = await controller.Update(99, new PricingItemRequest
        {
            Title = "Portraits",
            Description = "Description"
        });

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public sealed class PricingControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsActivePricingItems()
    {
        var expected = new[]
        {
            new PricingItemResponse(1, "Portrait", "Session", "Fixed", "100 лв.", 1, true, DateTime.UtcNow)
        };
        var service = new StubPricingService
        {
            ActiveHandler = () => Task.FromResult<IReadOnlyList<PricingItemResponse>>(expected)
        };
        var controller = new PricingController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
    }
}

public sealed class AdminPrintRequestsControllerTests
{
    [Fact]
    public async Task UpdateStatus_ForwardsIdentifierAndDto()
    {
        int capturedId = 0;
        UpdatePrintRequestStatusDto? capturedDto = null;
        var service = new StubAdminPrintRequestService
        {
            StatusHandler = (id, dto) =>
            {
                capturedId = id;
                capturedDto = dto;
                return Task.FromResult(ControllerServiceResult.Ok());
            }
        };
        var controller = new AdminPrintRequestsController(service);
        ControllerTestContext.Attach(controller);
        var dto = new UpdatePrintRequestStatusDto();

        var result = await controller.UpdateStatus(12, dto);

        result.Should().BeOfType<OkResult>();
        capturedId.Should().Be(12);
        capturedDto.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task MarkAllSeen_MapsNoContent()
    {
        var controller = new AdminPrintRequestsController(new StubAdminPrintRequestService());
        ControllerTestContext.Attach(controller);

        var result = await controller.MarkAllSeen();

        result.Should().BeOfType<NoContentResult>();
    }
}

public sealed class ClientPrintRequestsControllerTests
{
    [Fact]
    public async Task Create_ReturnsCreatedAtActionAndPassesAuthenticatedPrincipal()
    {
        ClaimsPrincipal? capturedPrincipal = null;
        CreatePrintRequestDto? capturedDto = null;
        var service = new StubClientPrintRequestEndpointService
        {
            CreateHandler = (principal, dto) =>
            {
                capturedPrincipal = principal;
                capturedDto = dto;
                return Task.FromResult(new ControllerServiceResult(
                    StatusCodes.Status201Created,
                    new CreatedPrintRequestResult(44)));
            }
        };
        var controller = new ClientPrintRequestsController(service);
        ControllerTestContext.Attach(controller, "client-1");
        var dto = new CreatePrintRequestDto();

        var result = await controller.Create(dto);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ClientPrintRequestsController.GetMineById));
        created.RouteValues!["id"].Should().Be(44);
        capturedPrincipal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("client-1");
        capturedDto.Should().BeSameAs(dto);
    }
}
