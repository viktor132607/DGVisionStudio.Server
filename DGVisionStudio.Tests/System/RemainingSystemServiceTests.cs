using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.System;

public sealed class AdminAuditLogQueryServiceTests
{
    [Fact]
    public async Task GetAuditLogsAsync_NormalizesPagingAndAppliesFilters()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Set<AuditLog>().AddRange(
            new AuditLog
            {
                AdminUserId = "admin-1",
                AdminEmail = "admin@example.com",
                Action = "UpdateGallery",
                EntityType = "Gallery",
                EntityId = "42",
                CreatedAtUtc = DateTime.UtcNow
            },
            new AuditLog
            {
                AdminUserId = "admin-2",
                AdminEmail = "other@example.com",
                Action = "DeleteUser",
                EntityType = "User",
                EntityId = "7",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
        await context.SaveChangesAsync();
        var service = new AdminAuditLogQueryService(context);

        var result = await service.GetAuditLogsAsync(
            page: 0,
            pageSize: 500,
            entityType: " Gallery ",
            entityId: " 42 ",
            adminEmail: " ADMIN@EXAMPLE ",
            action: " update ");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var value = result.Value;
        value.Should().NotBeNull();
        value!.GetType().GetProperty("page")!.GetValue(value).Should().Be(1);
        value.GetType().GetProperty("pageSize")!.GetValue(value).Should().Be(50);
        value.GetType().GetProperty("total")!.GetValue(value).Should().Be(1);
    }

    [Fact]
    public async Task GetAuditLogByIdAsync_ReturnsNotFound_ForUnknownId()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminAuditLogQueryService(context);

        var result = await service.GetAuditLogByIdAsync(999);

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task LogAsync_PersistsMetadataAndSerializesStructuredValues()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AuditLogService(context, NullLogger<AuditLogService>.Instance);

        await service.LogAsync(
            "admin-1",
            "admin@example.com",
            "UpdateGallery",
            "Gallery",
            "42",
            new { Title = "Before" },
            new { Title = "After" },
            "127.0.0.1",
            "tests",
            "trace-1");

        var stored = await context.Set<AuditLog>().SingleAsync();
        stored.AdminUserId.Should().Be("admin-1");
        stored.Action.Should().Be("UpdateGallery");
        stored.OldValue.Should().Contain("Before");
        stored.NewValue.Should().Contain("After");
        stored.TraceId.Should().Be("trace-1");
    }
}

public sealed class CsrfTokenServiceTests
{
    [Fact]
    public void GenerateToken_ReturnsDistinctNonEmptyTokens()
    {
        var service = new CsrfTokenService();

        var first = service.GenerateToken();
        var second = service.GenerateToken();

        first.Should().NotBeNullOrWhiteSpace();
        first.Length.Should().BeGreaterThan(20);
        second.Should().NotBe(first);
    }
}

public sealed class DebugUserServiceTests
{
    [Fact]
    public async Task GetUsersAsync_ProjectsUsersAndRoles()
    {
        var user = TestUsers.Create("user@example.com", "user-1");
        user.EmailConfirmed = true;
        var manager = new ConfigurableUserManager([user]);
        manager.SetRoles(user, "User", "Admin");
        var service = new DebugUserService(manager);

        var result = await service.GetUsersAsync();

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<List<object>>()
            .Which.Should().ContainSingle();
    }
}

public sealed class HealthServiceTests
{
    [Fact]
    public async Task HealthAndReadiness_ReturnEnvironmentAndDatabaseStatus()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var environment = new TestWebHostEnvironment { EnvironmentName = "Tests" };
        var service = new HealthService(environment, fixture.Context);

        var health = service.GetHealth();
        var readiness = await service.GetReadinessAsync();

        health.StatusCode.Should().Be(StatusCodes.Status200OK);
        readiness.StatusCode.Should().Be(StatusCodes.Status200OK);
        health.Value.Should().NotBeNull();
        readiness.Value.Should().NotBeNull();
    }
}

public sealed class HomeStatusServiceTests
{
    [Fact]
    public void GetStatus_ReturnsRunningMessage()
    {
        var result = new HomeStatusService().GetStatus();

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().NotBeNull();
        result.Value!.GetType().GetProperty("message")!.GetValue(result.Value)
            .Should().Be("DG Vision Studio API running");
    }
}

public sealed class SiteSettingsServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsSettingsOrderedByKey()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.SiteSettings.AddRange(
            new SiteSetting { Key = "z-setting", Value = "2", Description = "Z" },
            new SiteSetting { Key = "a-setting", Value = "1", Description = "A" });
        await context.SaveChangesAsync();
        var service = new SiteSettingsService(context);

        var result = await service.GetAllAsync();

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var items = result.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        items.Should().HaveCount(2);
        items[0].GetType().GetProperty("Key")!.GetValue(items[0]).Should().Be("a-setting");
    }
}

public sealed class TestimonialServiceTests
{
    [Fact]
    public async Task CreatePublishFilterUpdateAndDelete_FollowsCrudLifecycle()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new TestimonialService(context);
        var testimonial = new Testimonial
        {
            ClientName = "Client",
            Content = "Excellent service",
            Rating = 5,
            IsPublished = true,
            DisplayOrder = 2
        };

        var created = await service.CreateAsync(testimonial);
        var published = await service.GetPublishedAsync();
        var updated = await service.UpdateAsync(testimonial.Id, new Testimonial
        {
            ClientName = "Updated Client",
            Content = "Updated content",
            Rating = 4,
            IsPublished = false,
            DisplayOrder = 1
        });
        var deleted = await service.DeleteAsync(testimonial.Id);

        created.StatusCode.Should().Be(StatusCodes.Status200OK);
        published.Value.Should().BeOfType<List<Testimonial>>()
            .Which.Should().ContainSingle(x => x.Id == testimonial.Id);
        updated.StatusCode.Should().Be(StatusCodes.Status200OK);
        testimonial.ClientName.Should().Be("Updated Client");
        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        (await context.Testimonials.CountAsync()).Should().Be(0);
    }
}
