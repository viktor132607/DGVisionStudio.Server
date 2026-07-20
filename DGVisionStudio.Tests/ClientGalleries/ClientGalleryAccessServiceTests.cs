using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryAccessServiceTests
{
    [Fact]
    public async Task GrantAccessAsync_CreatesAccessAndEnablesClientAccess()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var setup = await SeedGalleryAndUserAsync(fixture.Context);
        var service = CreateService(fixture.Context, setup.User);
        var expiresAt = DateTime.UtcNow.AddDays(3);

        var success = await service.GrantAccessAsync(setup.Album.Id, new GrantGalleryAccessRequest
        {
            UserEmail = $"  {setup.User.Email}  ",
            PreviewEnabled = true,
            DownloadEnabled = true,
            DownloadExpiresAtUtc = expiresAt
        });

        success.Should().BeTrue();
        var album = await fixture.Context.PortfolioAlbums.SingleAsync();
        album.AllowClientAccess.Should().BeTrue();
        var access = await fixture.Context.UserAlbumAccesses.SingleAsync();
        access.UserId.Should().Be(setup.User.Id);
        access.PreviewEnabled.Should().BeTrue();
        access.DownloadEnabled.Should().BeTrue();
        access.DownloadExpiresAtUtc.Should().Be(expiresAt);
    }

    [Fact]
    public async Task UpdateAccessAsync_UpdatesExistingPermissions()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var setup = await SeedGalleryAndUserAsync(fixture.Context, includeAccess: true);
        var service = CreateService(fixture.Context, setup.User);
        var expiresAt = DateTime.UtcNow.AddDays(1);

        var success = await service.UpdateAccessAsync(setup.Album.Id, setup.User.Id, new UpdateGalleryAccessRequest
        {
            PreviewEnabled = false,
            DownloadEnabled = true,
            DownloadExpiresAtUtc = expiresAt
        });

        success.Should().BeTrue();
        var access = await fixture.Context.UserAlbumAccesses.SingleAsync();
        access.PreviewEnabled.Should().BeFalse();
        access.DownloadEnabled.Should().BeTrue();
        access.DownloadExpiresAtUtc.Should().Be(expiresAt);
    }

    [Fact]
    public async Task RemoveAccessAsync_RemovesExistingAccess()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var setup = await SeedGalleryAndUserAsync(fixture.Context, includeAccess: true);
        var service = CreateService(fixture.Context, setup.User);

        var success = await service.RemoveAccessAsync(setup.Album.Id, setup.User.Id);

        success.Should().BeTrue();
        (await fixture.Context.UserAlbumAccesses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetGalleryAccessesAsync_ReturnsUserEmailAndPermissions()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var setup = await SeedGalleryAndUserAsync(fixture.Context, includeAccess: true);
        var service = CreateService(fixture.Context, setup.User);

        var accesses = await service.GetGalleryAccessesAsync(setup.Album.Id);

        accesses.Should().ContainSingle();
        accesses[0].UserId.Should().Be(setup.User.Id);
        accesses[0].Email.Should().Be(setup.User.Email);
        accesses[0].PreviewEnabled.Should().BeTrue();
    }

    private static ClientGalleryAccessService CreateService(AppDbContext context, ApplicationUser user) =>
        new(
            context,
            new TestUserManager(user),
            NullLogger<ClientGalleryAccessService>.Instance);

    private static async Task<(PortfolioAlbum Album, ApplicationUser User)> SeedGalleryAndUserAsync(
        AppDbContext context,
        bool includeAccess = false)
    {
        var category = new PortfolioCategory
        {
            Key = $"access-{Guid.NewGuid():N}",
            Name = "Access",
            NameEn = "Access"
        };
        var user = TestUsers.Create("gallery-user@example.com");
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Title = "Gallery",
            Slug = $"gallery-{Guid.NewGuid():N}",
            AllowClientAccess = false
        };

        context.Users.Add(user);
        context.PortfolioAlbums.Add(album);
        if (includeAccess)
        {
            context.UserAlbumAccesses.Add(new UserAlbumAccess
            {
                PortfolioAlbum = album,
                User = user,
                UserId = user.Id,
                PreviewEnabled = true,
                DownloadEnabled = false
            });
        }

        await context.SaveChangesAsync();
        return (album, user);
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private SqliteFixture(SqliteConnection connection, AppDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public AppDbContext Context { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new SqliteFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
