using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.ContactRequests;

public sealed class ContactRequestServiceRefactorTests
{
    [Theory]
    [InlineData("+359 888 123 456", true)]
    [InlineData("0888123456", true)]
    [InlineData("abc123", false)]
    [InlineData("+1", false)]
    public void Validator_NormalizesAndValidatesPhone(
        string phone,
        bool valid)
    {
        var result = new ContactRequestInputValidator()
            .Validate(new CreateContactRequestDto
            {
                Name = "  Client  ",
                Email = "  client@example.com  ",
                Phone = phone,
                Subject = "  Subject  ",
                Message = " "
            });

        result.IsValid.Should().Be(valid);

        if (!valid)
            return;

        result.Input!.Name.Should().Be("Client");
        result.Input.Email.Should().Be("client@example.com");
        result.Input.Phone.Should().Be(phone.Trim());
        result.Input.Subject.Should().Be("Subject");
        result.Input.Message.Should().Be("-");
    }

    [Fact]
    public async Task NotificationService_LogsSuccessfulEscapedEmail()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var email = new RecordingEmailService();
        var service = new ContactRequestNotificationService(
            context,
            email,
            TestConfiguration.Create(
                ("Resend:OwnerEmail", "owner@example.com")));

        var request = Request("<Client>");
        request.Message = "<b>Hello</b>\nNext";
        context.ContactRequests.Add(request);
        await context.SaveChangesAsync();

        await service.SendOwnerNotificationAsync(request);

        email.Messages.Should().ContainSingle();
        email.Messages[0].To.Should().Be("owner@example.com");
        email.Messages[0].Body.Should().Contain("&lt;Client&gt;");
        email.Messages[0].Body.Should().Contain(
            "&lt;b&gt;Hello&lt;/b&gt;<br />Next");

        var log = await context.EmailLogs.SingleAsync();
        log.IsSent.Should().BeTrue();
        log.SentAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task NotificationService_LogsFailureWithoutThrowing()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var email = new RecordingEmailService
        {
            ExceptionToThrow =
                new InvalidOperationException("mail failed")
        };

        var service = new ContactRequestNotificationService(
            context,
            email,
            TestConfiguration.Create(
                ("Resend:OwnerEmail", "owner@example.com")));

        var request = Request("Client");
        context.ContactRequests.Add(request);
        await context.SaveChangesAsync();

        await service.SendOwnerNotificationAsync(request);

        var log = await context.EmailLogs.SingleAsync();
        log.IsSent.Should().BeFalse();
        log.ErrorMessage.Should().Contain("mail failed");
    }

    [Fact]
    public async Task QueryService_OrdersNewestFirstAndReturnsNotFound()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var older = Request("Older");
        older.CreatedAtUtc = DateTime.UtcNow.AddDays(-1);
        var newer = Request("Newer");
        newer.CreatedAtUtc = DateTime.UtcNow;

        context.ContactRequests.AddRange(older, newer);
        await context.SaveChangesAsync();

        var service = new ContactRequestQueryService(context);

        var all = await service.GetAllAsync();
        var missing = await service.GetAsync(Guid.NewGuid());

        var items = all.Value
            .Should()
            .BeAssignableTo<List<ContactRequest>>()
            .Subject;

        items.Select(x => x.Id)
            .Should()
            .Equal(newer.Id, older.Id);

        missing.StatusCode.Should().Be(
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AdminCommands_UpdateStatusArchivesFinalStatesAndDeletes()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var request = Request("Client");
        context.ContactRequests.Add(request);
        await context.SaveChangesAsync();

        var service =
            new ContactRequestAdminCommandService(context);

        var updated = await service.UpdateAsync(
            request.Id,
            new UpdateContactRequestDto
            {
                Status = ContactRequestStatus.InProgress,
                AdminComment = "  note  ",
                IsArchived = false
            });

        updated.StatusCode.Should().Be(StatusCodes.Status200OK);
        request.Status.Should().Be(ContactRequestStatus.InProgress);
        request.AdminComment.Should().Be("  note  ");

        var completed = await service.UpdateStatusAsync(
            request.Id,
            new UpdateContactRequestDto
            {
                Status = ContactRequestStatus.Completed
            });

        completed.StatusCode.Should().Be(StatusCodes.Status200OK);
        request.IsArchived.Should().BeTrue();

        (await service.DeleteAsync(request.Id))
            .StatusCode.Should().Be(
                StatusCodes.Status204NoContent);

        (await context.ContactRequests.CountAsync())
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task Facade_DelegatesSubmissionQueryAndAdminCommands()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var facade = new ContactRequestService(context);

        var created = await facade.CreateAsync(
            new CreateContactRequestDto
            {
                Name = "Client",
                Email = "client@example.com",
                Phone = "+359888123456",
                Message = "Hello"
            });

        created.StatusCode.Should().Be(StatusCodes.Status200OK);

        var request = await context.ContactRequests.SingleAsync();

        (await facade.GetAsync(request.Id))
            .StatusCode.Should().Be(StatusCodes.Status200OK);

        (await facade.MarkAllSeenAsync())
            .StatusCode.Should().Be(
                StatusCodes.Status204NoContent);

        request.IsSeenByAdmin.Should().BeTrue();
    }

    private static ContactRequest Request(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = "client@example.com",
            Phone = "+359888123456",
            Message = "Hello",
            CreatedAtUtc = DateTime.UtcNow
        };
}
