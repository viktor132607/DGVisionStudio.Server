using System.Reflection;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Infrastructure;

public sealed class CloudinaryFileStorageServiceTests
{
    [Fact]
    public void Constructor_Throws_WhenCredentialsAreMissing()
    {
        var action = () => new CloudinaryFileStorageService(TestConfiguration.Create());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cloudinary:CloudName*");
    }

    [Fact]
    public async Task SaveImageAsync_RejectsUnsupportedExtensionWithoutCallingCloudinary()
    {
        var service = CreateCloudinary();

        var action = () => service.SaveImageAsync(
            new MemoryStream([1, 2, 3]),
            "payload.exe",
            "uploads/portfolio");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unsupported image format.");
    }

    [Fact]
    public async Task SaveImageAsync_ReturnsNormalizedPath_WhenSeekableFileExceedsCloudinaryLimit()
    {
        var service = CreateCloudinary();
        await using var stream = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);

        var result = await service.SaveImageAsync(
            stream,
            "large.JPG",
            "/uploads/portfolio/albums/");

        result.Should().Be("portfolio/albums/large.JPG");
    }

    private static CloudinaryFileStorageService CreateCloudinary() => new(
        TestConfiguration.Create(
            ("Cloudinary:CloudName", "test-cloud"),
            ("Cloudinary:ApiKey", "test-key"),
            ("Cloudinary:ApiSecret", "test-secret"),
            ("Cloudinary:Folder", "dgvisionstudio/portfolio")));
}

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dg-local-storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveOpenExistsAndDelete_UsesWebRootLifecycle()
    {
        var service = CreateService();
        await using var input = new MemoryStream([10, 20, 30]);

        var path = await service.SaveFileAsync(input, "photo.PNG", "uploads/gallery");

        path.Should().StartWith("/uploads/gallery/").And.EndWith(".png");
        (await service.FileExistsAsync(path)).Should().BeTrue();
        await using var opened = await service.OpenReadAsync(path);
        opened.Should().NotBeNull();
        using var memory = new MemoryStream();
        await opened!.CopyToAsync(memory);
        memory.ToArray().Should().Equal(10, 20, 30);

        await service.DeleteFileAsync(path);
        (await service.FileExistsAsync(path)).Should().BeFalse();
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("uploads/../../outside.txt")]
    public async Task StorageOperations_RejectPathsOutsideWebRoot(string path)
    {
        var service = CreateService();

        var exists = () => service.FileExistsAsync(path);
        var open = () => service.OpenReadAsync(path);
        var delete = () => service.DeleteFileAsync(path);

        await exists.Should().ThrowAsync<InvalidOperationException>();
        await open.Should().ThrowAsync<InvalidOperationException>();
        await delete.Should().ThrowAsync<InvalidOperationException>();
    }

    private LocalFileStorageService CreateService()
    {
        Directory.CreateDirectory(_root);
        return new LocalFileStorageService(new TestWebHostEnvironment
        {
            ContentRootPath = _root,
            WebRootPath = Path.Combine(_root, "wwwroot")
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

public sealed class AdminCalendarServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsBlankTitleAndInvalidDateRange()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminCalendarService(context);

        var blank = await service.CreateAsync(new CalendarEventDto
        {
            Title = " ",
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1)
        });
        var invalidRange = await service.CreateAsync(new CalendarEventDto
        {
            Title = "Session",
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow
        });

        blank.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        invalidRange.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateAsync_NormalizesFieldsAndIgnoresMissingContactRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminCalendarService(context);
        var start = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Unspecified);

        var result = await service.CreateAsync(new CalendarEventDto
        {
            Title = "  Portrait session  ",
            EventType = " ",
            ClientEmail = "  client@example.com  ",
            ContactRequestId = Guid.NewGuid(),
            StartAtUtc = start,
            EndAtUtc = start.AddHours(2)
        });

        result.StatusCode.Should().Be(StatusCodes.Status201Created);
        var stored = await context.CalendarEvents.SingleAsync();
        stored.Title.Should().Be("Portrait session");
        stored.EventType.Should().Be("Photoshoot");
        stored.ClientEmail.Should().Be("client@example.com");
        stored.ContactRequestId.Should().BeNull();
        stored.StartAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }
}

public sealed class CalendarReminderEmailServiceTests
{
    [Fact]
    public async Task SendDueReminders_SendsTwoHourReminderAndPersistsLog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var calendarEvent = new CalendarEvent
        {
            Title = "<Portrait>",
            EventType = "Photoshoot",
            ClientName = "Client",
            ClientEmail = "client@example.com",
            StartAtUtc = DateTime.UtcNow.AddMinutes(45),
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            RemindersEnabled = true
        };
        context.CalendarEvents.Add(calendarEvent);
        await context.SaveChangesAsync();
        var email = new RecordingEmailService();
        using var provider = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton<IEmailService>(email)
            .BuildServiceProvider();
        var worker = new CalendarReminderEmailService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CalendarReminderEmailService>.Instance);
        var method = typeof(CalendarReminderEmailService).GetMethod(
            "SendDueReminders",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)method.Invoke(worker, [CancellationToken.None])!;

        email.Messages.Should().ContainSingle();
        email.Messages.Single().Subject.Should().Contain("2 часа");
        email.Messages.Single().Body.Should().Contain("&lt;Portrait&gt;");
        (await context.EmailLogs.SingleAsync()).IsSent.Should().BeTrue();
        (await context.CalendarEvents.SingleAsync()).Reminder2hSentAtUtc.Should().NotBeNull();
    }
}

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendAsync_ThrowsBeforeNetworkCall_WhenResendSettingsAreMissing()
    {
        var service = new EmailService(TestConfiguration.Create());

        var action = () => service.SendAsync("client@example.com", "Subject", "Body");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Resend settings are not configured.");
    }
}
