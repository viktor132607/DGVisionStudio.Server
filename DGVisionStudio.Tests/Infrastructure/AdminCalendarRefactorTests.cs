using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Infrastructure;

public sealed class AdminCalendarRefactorTests
{
    [Fact]
    public async Task QueryService_ReturnsEventsOrderedByStartAndHandlesMissingEvent()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var late = Event("Late", DateTime.UtcNow.AddHours(4));
        var early = Event("Early", DateTime.UtcNow.AddHours(1));
        context.CalendarEvents.AddRange(late, early);
        await context.SaveChangesAsync();

        var service = new AdminCalendarQueryService(context);

        var all = await service.GetAllAsync();
        var missing = await service.GetAsync(999999);

        all.StatusCode.Should().Be(StatusCodes.Status200OK);
        all.Value.Should().BeAssignableTo<IEnumerable<CalendarEvent>>()
            .Which.Select(x => x.Title).Should().Equal("Early", "Late");
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ContactRequestQueryService_FiltersArchivedFromListButCanFetchArchivedById()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var active = Contact("active@example.com", archived: false);
        var archived = Contact("archived@example.com", archived: true);
        context.ContactRequests.AddRange(active, archived);
        await context.SaveChangesAsync();

        var service = new AdminCalendarContactRequestQueryService(context);

        var list = await service.GetForImportAsync();
        var archivedById = await service.GetForImportAsync(archived.Id);

        list.StatusCode.Should().Be(StatusCodes.Status200OK);
        list.Value.Should().BeAssignableTo<IEnumerable<object>>()
            .Which.Should().ContainSingle();
        archivedById.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CommandService_UpdateNormalizesValuesAndResetsSentReminders()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var contact = Contact("client@example.com", archived: false);
        var item = Event("Original", DateTime.UtcNow.AddDays(1));
        item.ClientEmail = "old@example.com";
        item.Reminder24hSentAtUtc = DateTime.UtcNow.AddHours(-2);
        item.Reminder2hSentAtUtc = DateTime.UtcNow.AddHours(-1);
        context.AddRange(contact, item);
        await context.SaveChangesAsync();

        var service = new AdminCalendarCommandService(
            context,
            new AdminCalendarEventInputService(context));
        var start = DateTime.SpecifyKind(
            DateTime.UtcNow.AddDays(2),
            DateTimeKind.Unspecified);

        var result = await service.UpdateAsync(
            item.Id,
            new CalendarEventDto
            {
                Title = "  Updated  ",
                EventType = " ",
                ClientEmail = "  client@example.com  ",
                ContactRequestId = contact.Id,
                StartAtUtc = start,
                EndAtUtc = start.AddHours(2),
                RemindersEnabled = true
            });

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await context.CalendarEvents.SingleAsync();
        stored.Title.Should().Be("Updated");
        stored.EventType.Should().Be("Photoshoot");
        stored.ClientEmail.Should().Be("client@example.com");
        stored.ContactRequestId.Should().Be(contact.Id);
        stored.StartAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        stored.Reminder24hSentAtUtc.Should().BeNull();
        stored.Reminder2hSentAtUtc.Should().BeNull();
        stored.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CommandService_DeletePreservesNotFoundAndNoContentSemantics()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var item = Event("Delete", DateTime.UtcNow.AddHours(2));
        context.CalendarEvents.Add(item);
        await context.SaveChangesAsync();
        var service = new AdminCalendarCommandService(
            context,
            new AdminCalendarEventInputService(context));

        var deleted = await service.DeleteAsync(item.Id);
        var missing = await service.DeleteAsync(item.Id);

        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await context.CalendarEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task FacadeCompatibilityConstructor_DelegatesCreateAndQueryOperations()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminCalendarService(context);
        var start = DateTime.UtcNow.AddDays(1);

        var created = await service.CreateAsync(new CalendarEventDto
        {
            Title = "Facade event",
            StartAtUtc = start,
            EndAtUtc = start.AddHours(1)
        });
        var all = await service.GetAllAsync();

        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        all.Value.Should().BeAssignableTo<IEnumerable<CalendarEvent>>()
            .Which.Should().ContainSingle(x => x.Title == "Facade event");
    }

    private static CalendarEvent Event(string title, DateTime start) => new()
    {
        Title = title,
        EventType = "Photoshoot",
        StartAtUtc = start,
        EndAtUtc = start.AddHours(1),
        RemindersEnabled = true
    };

    private static ContactRequest Contact(string email, bool archived) => new()
    {
        Name = "Client",
        Email = email,
        Message = "Message",
        IsArchived = archived,
        CreatedAtUtc = DateTime.UtcNow
    };
}
