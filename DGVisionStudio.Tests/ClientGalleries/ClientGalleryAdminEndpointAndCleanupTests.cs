using System.Security.Claims;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryAdminQueryServiceTests
{
    [Fact]
    public async Task GetAllGalleriesAsync_ReturnsEmptyList_WhenDatabaseIsEmpty()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var service = new ClientGalleryAdminQueryService(
            db,
            new TestUserManager(null),
            new ClientGalleryMapper());

        var result = await service.GetAllGalleriesAsync();

        result.Should().BeEmpty();
    }
}

public sealed class ClientGalleryAdminCommandServiceTests
{
    [Fact]
    public async Task CreateGalleryAsync_CreatesNormalizedAdminGallery()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new ClientGalleryAdminCommandService(
            fixture.Context,
            new StubClientGalleryAccessService(),
            new ClientGalleryNamingService(fixture.Context),
            NullLogger<ClientGalleryAdminCommandService>.Instance);

        var id = await service.CreateGalleryAsync(new AdminCreateClientGalleryRequest
        {
            Title = "  New Gallery  ",
            Description = "  Description  ",
            IsActive = true,
            IsPublic = false
        });

        id.Should().BeGreaterThan(0);
        var album = await fixture.Context.PortfolioAlbums.SingleAsync();
        album.Title.Should().Be("New Gallery");
        album.Description.Should().Be("Description");
        album.AllowClientAccess.Should().BeTrue();
        album.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteGalleryAsync_ReturnsFalse_WhenGalleryDoesNotExist()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new ClientGalleryAdminCommandService(
            fixture.Context,
            new StubClientGalleryAccessService(),
            new ClientGalleryNamingService(fixture.Context),
            NullLogger<ClientGalleryAdminCommandService>.Instance);

        (await service.DeleteGalleryAsync(999)).Should().BeFalse();
    }
}

public sealed class ClientGalleryAdminServiceTests
{
    [Fact]
    public async Task GetAllGalleriesAsync_DelegatesToQueryService()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var mapper = new ClientGalleryMapper();
        var service = new ClientGalleryAdminService(
            new ClientGalleryAdminQueryService(
                fixture.Context,
                new TestUserManager(null),
                mapper),
            new ClientGalleryAdminCommandService(
                fixture.Context,
                new StubClientGalleryAccessService(),
                new ClientGalleryNamingService(fixture.Context),
                NullLogger<ClientGalleryAdminCommandService>.Instance));

        var result = await service.GetAllGalleriesAsync();

        result.Should().BeEmpty();
    }
}

public sealed class ClientGalleryEndpointServiceTests
{
    [Fact]
    public async Task GetMyGalleriesAsync_ReturnsUnauthorized_WhenUserIsMissing()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var service = new ClientGalleryEndpointService(
            new StubClientGalleryService(),
            new TestUserManager(null),
            db,
            new StubFileStorageService());

        var result = await service.GetMyGalleriesAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetMyGalleriesAsync_ReturnsServiceData_ForAuthenticatedUser()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("user@test.bg", "user-1");
        var expected = new List<MyClientGalleryDto> { new() { Id = 42, Title = "Gallery" } };
        var gallery = new StubClientGalleryService
        {
            GetMyGalleries = id => Task.FromResult(id == user.Id ? expected : [])
        };
        var service = new ClientGalleryEndpointService(
            gallery,
            new TestUserManager(user),
            db,
            new StubFileStorageService());

        var result = await service.GetMyGalleriesAsync(TestUsers.CreatePrincipal(user));

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeSameAs(expected);
    }
}

public sealed class ClientGalleryServiceTests
{
    [Fact]
    public async Task GetAllGalleriesAsync_DelegatesToAdminService()
    {
        var expected = new List<MyClientGalleryDto> { new() { Id = 9, Title = "Delegated" } };
        var admin = new StubClientGalleryAdminService { Galleries = expected };
        var service = new ClientGalleryService(
            admin,
            new StubClientGalleryUserService(),
            new StubClientGalleryAccessService(),
            new StubClientGalleryPhotoService(),
            new StubClientGalleryExpiryService());

        var result = await service.GetAllGalleriesAsync();

        result.Should().BeSameAs(expected);
    }
}

public sealed class ExpiredGalleryCleanupServiceTests
{
    [Fact]
    public async Task BackgroundCycle_InvokesBothGalleryExpiryOperations()
    {
        var marked = 0;
        var deleted = 0;
        var cycleCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gallery = new StubClientGalleryService
        {
            MarkExpired = _ =>
            {
                Interlocked.Increment(ref marked);
                return Task.FromResult(1);
            },
            DeleteExpired = _ =>
            {
                Interlocked.Increment(ref deleted);
                cycleCompleted.TrySetResult();
                return Task.FromResult(1);
            }
        };
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"cleanup-{Guid.NewGuid():N}"));
        services.AddSingleton<IClientGalleryService>(gallery);
        await using var provider = services.BuildServiceProvider();
        var worker = new ExpiredGalleryCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExpiredGalleryCleanupService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await cycleCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        marked.Should().Be(1);
        deleted.Should().Be(1);
    }
}
