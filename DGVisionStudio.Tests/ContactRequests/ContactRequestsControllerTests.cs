using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Tests.ContactRequests;

public sealed class ContactRequestsControllerTests
{
    [Fact]
    public async Task Create_ReturnsBadRequest_WhenPhoneIsMissing()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var dto = CreateDto(name: "John Doe", email: "john@example.com", phone: " ");

        var result = await InvokeCreate(controller, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.ContactRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenPhoneIsInvalid()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var dto = CreateDto(name: "John Doe", email: "john@example.com", phone: "abc123");

        var result = await InvokeCreate(controller, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.ContactRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_SavesTrimmedRequest_AndUsesDefaultMessage_WhenMessageIsBlank()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var dto = CreateDto(
            name: "  John Doe  ",
            email: "  john@example.com  ",
            phone: "  +359 888 123 456  ",
            subject: "  Photoshoot  ",
            message: " ");

        var result = await InvokeCreate(controller, dto);

        result.Should().BeOfType<OkObjectResult>();
        var request = await context.ContactRequests.SingleAsync();
        request.Name.Should().Be("John Doe");
        request.Email.Should().Be("john@example.com");
        request.Phone.Should().Be("+359 888 123 456");
        request.Subject.Should().Be("Photoshoot");
        request.Message.Should().Be("-");
        request.IsSeenByAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllSeen_MarksOnlyUnseenNonArchivedRequests()
    {
        await using var context = CreateContext();
        var visibleUnseen = new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Visible",
            Email = "visible@example.com",
            Phone = "+359888123456",
            Message = "Visible",
            IsSeenByAdmin = false,
            IsArchived = false
        };
        var archivedUnseen = new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Archived",
            Email = "archived@example.com",
            Phone = "+359888123457",
            Message = "Archived",
            IsSeenByAdmin = false,
            IsArchived = true
        };
        var alreadySeen = new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Seen",
            Email = "seen@example.com",
            Phone = "+359888123458",
            Message = "Seen",
            IsSeenByAdmin = true,
            IsArchived = false
        };
        context.ContactRequests.AddRange(visibleUnseen, archivedUnseen, alreadySeen);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);

        var result = await controller.MarkAllSeen();

        result.Should().BeOfType<NoContentResult>();
        var requests = await context.ContactRequests.ToDictionaryAsync(x => x.Id);
        requests[visibleUnseen.Id].IsSeenByAdmin.Should().BeTrue();
        requests[visibleUnseen.Id].UpdatedAtUtc.Should().NotBeNull();
        requests[archivedUnseen.Id].IsSeenByAdmin.Should().BeFalse();
        requests[alreadySeen.Id].IsSeenByAdmin.Should().BeTrue();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static ContactRequestsController CreateController(AppDbContext context)
    {
        var configuration = new ConfigurationBuilder().Build();

        return (ContactRequestsController)Activator.CreateInstance(
            typeof(ContactRequestsController),
            context,
            null!,
            configuration)!;
    }

    private static object CreateDto(string name, string email, string phone, string? subject = null, string? message = null)
    {
        var dtoType = typeof(ContactRequestsController)
            .GetMethod(nameof(ContactRequestsController.Create))!
            .GetParameters()[0]
            .ParameterType;

        var dto = Activator.CreateInstance(dtoType)!;
        SetProperty(dto, "Name", name);
        SetProperty(dto, "Email", email);
        SetProperty(dto, "Phone", phone);
        SetProperty(dto, "Subject", subject);
        SetProperty(dto, "Message", message);
        return dto;
    }

    private static async Task<IActionResult> InvokeCreate(ContactRequestsController controller, object dto)
    {
        var resultTask = (Task<IActionResult>)typeof(ContactRequestsController)
            .GetMethod(nameof(ContactRequestsController.Create))!
            .Invoke(controller, new[] { dto })!;

        return await resultTask;
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        target.GetType().GetProperty(propertyName)!.SetValue(target, value);
    }
}
