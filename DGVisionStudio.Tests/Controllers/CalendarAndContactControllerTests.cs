using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Controllers;

public sealed class AdminCalendarControllerTests
{
    [Fact]
    public async Task Create_ReturnsCreatedAtAction_ForCreatedCalendarEvent()
    {
        CalendarEventDto? captured = null;
        var service = new StubAdminCalendarService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new ControllerServiceResult(
                    StatusCodes.Status201Created,
                    new CalendarEvent { Id = 17, Title = dto.Title }));
            }
        };
        var controller = new AdminCalendarController(service);
        ControllerTestContext.Attach(controller);
        var dto = new CalendarEventDto
        {
            Title = "Session",
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1)
        };

        var result = await controller.Create(dto);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(AdminCalendarController.Get));
        created.RouteValues!["id"].Should().Be(17);
        captured.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetContactRequestForImport_ForwardsIdentifier()
    {
        var expected = Guid.NewGuid();
        Guid captured = default;
        var service = new StubAdminCalendarService
        {
            ContactRequestHandler = id =>
            {
                captured = id;
                return Task.FromResult(ControllerServiceResult.NotFound());
            }
        };
        var controller = new AdminCalendarController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.GetContactRequestForImport(expected);

        result.Should().BeOfType<NotFoundResult>();
        captured.Should().Be(expected);
    }
}

public sealed class AdminContactRequestsControllerTests
{
    [Fact]
    public async Task UpdateStatus_ForwardsIdentifierAndDto()
    {
        var expectedId = Guid.NewGuid();
        Guid capturedId = default;
        UpdateContactRequestDto? capturedDto = null;
        var service = new StubContactRequestService
        {
            StatusHandler = (id, dto) =>
            {
                capturedId = id;
                capturedDto = dto;
                return Task.FromResult(ControllerServiceResult.NoContent());
            }
        };
        var controller = new AdminContactRequestsController(service);
        ControllerTestContext.Attach(controller);
        var dto = new UpdateContactRequestDto();

        var result = await controller.UpdateStatus(expectedId, dto);

        result.Should().BeOfType<NoContentResult>();
        capturedId.Should().Be(expectedId);
        capturedDto.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task Delete_MapsNotFoundResult()
    {
        var service = new StubContactRequestService
        {
            DeleteHandler = _ => Task.FromResult(ControllerServiceResult.NotFound(new { message = "missing" }))
        };
        var controller = new AdminContactRequestsController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
