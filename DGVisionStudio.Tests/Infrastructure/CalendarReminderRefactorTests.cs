using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Infrastructure;

public sealed class CalendarReminderRefactorTests
{
    [Fact]
    public async Task DueQuery_ClassifiesTwoHourAndTwentyFourHourReminders()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var now = DateTime.UtcNow;

        context.CalendarEvents.AddRange(
            Event(now.AddHours(1), "two@example.com"),
            Event(now.AddHours(8), "day@example.com"),
            Event(now.AddHours(26), "later@example.com"),
            Event(now.AddHours(1), "disabled@example.com", remindersEnabled: false));

        await context.SaveChangesAsync();

        var reminders = await new CalendarReminderDueQueryService(context)
            .GetDueAsync(now);

        reminders.Should().HaveCount(2);
        reminders.Should().ContainSingle(x =>
            x.Kind == CalendarReminderKind.TwoHours &&
            x.CalendarEvent.ClientEmail == "two@example.com");
        reminders.Should().ContainSingle(x =>
            x.Kind == CalendarReminderKind.TwentyFourHours &&
            x.CalendarEvent.ClientEmail == "day@example.com");
    }

    [Fact]
    public void Composer_EscapesClientContentAndBuildsExpectedSubjects()
    {
        var composer = new CalendarReminderMessageComposer(
            NullLogger<CalendarReminderMessageComposer>.Instance);
        var calendarEvent = Event(
            DateTime.UtcNow.AddHours(3),
            " client@example.com ");
        calendarEvent.Title = "<Portrait>";
        calendarEvent.ClientName = "<Client>";
        calendarEvent.Description = "<b>unsafe</b>\nnext";

        var twoHour = composer.Compose(
            calendarEvent,
            CalendarReminderKind.TwoHours);
        var twentyFourHour = composer.Compose(
            calendarEvent,
            CalendarReminderKind.TwentyFourHours);

        twoHour.Should().NotBeNull();
        twoHour!.ToEmail.Should().Be("client@example.com");
        twoHour.Subject.Should().Contain("2 часа");
        twentyFourHour!.Subject.Should().Contain("утре");
        twoHour.Body.Should().Contain("&lt;Portrait&gt;");
        twoHour.Body.Should().Contain("&lt;Client&gt;");
        twoHour.Body.Should().Contain("&lt;b&gt;unsafe&lt;/b&gt;<br />next");
    }

    [Fact]
    public async Task FailedDelivery_PersistsFailureAndLeavesReminderForRetry()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var calendarEvent = Event(
            DateTime.UtcNow.AddMinutes(30),
            "client@example.com");
        context.CalendarEvents.Add(calendarEvent);
        await context.SaveChangesAsync();

        var email = new RecordingEmailService
        {
            ExceptionToThrow = new InvalidOperationException("Resend failed")
        };
        var processor = CalendarReminderTestFactory.CreateProcessor(
            context,
            email);

        var sent = await processor.ProcessDueAsync();

        sent.Should().Be(0);
        calendarEvent.Reminder2hSentAtUtc.Should().BeNull();
        var log = await context.EmailLogs.SingleAsync();
        log.IsSent.Should().BeFalse();
        log.ErrorMessage.Should().Contain("Resend failed");
    }

    [Fact]
    public async Task SuccessfulDelivery_SetsOnlyRelevantReminderMarker()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var calendarEvent = Event(
            DateTime.UtcNow.AddHours(6),
            "client@example.com");
        context.CalendarEvents.Add(calendarEvent);
        await context.SaveChangesAsync();

        var processor = CalendarReminderTestFactory.CreateProcessor(
            context,
            new RecordingEmailService());

        var sent = await processor.ProcessDueAsync();

        sent.Should().Be(1);
        calendarEvent.Reminder24hSentAtUtc.Should().NotBeNull();
        calendarEvent.Reminder2hSentAtUtc.Should().BeNull();
        (await context.EmailLogs.SingleAsync()).IsSent.Should().BeTrue();
    }

    private static CalendarEvent Event(
        DateTime start,
        string email,
        bool remindersEnabled = true) =>
        new()
        {
            Title = "Session",
            EventType = "Photoshoot",
            ClientEmail = email,
            StartAtUtc = start,
            EndAtUtc = start.AddHours(1),
            RemindersEnabled = remindersEnabled
        };
}

internal static class CalendarReminderTestFactory
{
    public static CalendarReminderProcessor CreateProcessor(
        AppDbContext context,
        IEmailService emailService)
    {
        var composer = new CalendarReminderMessageComposer(
            NullLogger<CalendarReminderMessageComposer>.Instance);
        var delivery = new CalendarReminderDeliveryService(
            context,
            emailService,
            composer,
            NullLogger<CalendarReminderDeliveryService>.Instance);

        return new CalendarReminderProcessor(
            context,
            new CalendarReminderDueQueryService(context),
            delivery,
            NullLogger<CalendarReminderProcessor>.Instance);
    }
}
