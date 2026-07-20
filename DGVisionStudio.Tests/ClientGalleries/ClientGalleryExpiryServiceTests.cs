using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryExpiryServiceTests
{
    [Fact]
    public async Task MarkExpiredUserGalleriesAsync_ExpiresOnlyPastDueUploads()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var category = CreateCategory();
        var expired = CreateUserUpload(category, DateTime.UtcNow.AddMinutes(-5));
        var active = CreateUserUpload(category, DateTime.UtcNow.AddHours(2));
        fixture.Context.AddRange(category, expired, active);
        await fixture.Context.SaveChangesAsync();
        var service = new ClientGalleryExpiryService(
            fixture.Context,
            NullLogger<ClientGalleryExpiryService>.Instance);

        var count = await service.MarkExpiredUserGalleriesAsync();

        count.Should().Be(1);
        var storedExpired = await fixture.Context.PortfolioAlbums
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == expired.Id);
        storedExpired.UserGalleryStatus.Should().Be(UserClientGalleryStatus.Expired);
        storedExpired.AllowClientAccess.Should().BeFalse();

        var storedActive = await fixture.Context.PortfolioAlbums.SingleAsync(x => x.Id == active.Id);
        storedActive.UserGalleryStatus.Should().Be(UserClientGalleryStatus.Pending);
        storedActive.AllowClientAccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExpiredUserGalleriesAsync_SoftDeletesGalleryAndImagesAfterGracePeriod()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var category = CreateCategory();
        var album = CreateUserUpload(category, DateTime.UtcNow.AddDays(-2));
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/uploads/client.jpg",
            IsPublished = true,
            IsCover = true
        });
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();
        var service = new ClientGalleryExpiryService(
            fixture.Context,
            NullLogger<ClientGalleryExpiryService>.Instance);

        var count = await service.DeleteExpiredUserGalleriesAsync();

        count.Should().Be(1);
        var storedAlbum = await fixture.Context.PortfolioAlbums
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == album.Id);
        storedAlbum.IsDeleted.Should().BeTrue();
        storedAlbum.DeletedAtUtc.Should().NotBeNull();
        storedAlbum.AllowClientAccess.Should().BeFalse();
        storedAlbum.IsPublished.Should().BeFalse();
        storedAlbum.UserGalleryStatus.Should().Be(UserClientGalleryStatus.Expired);

        var storedImage = await fixture.Context.PortfolioImages
            .IgnoreQueryFilters()
            .SingleAsync();
        storedImage.IsDeleted.Should().BeTrue();
        storedImage.IsPublished.Should().BeFalse();
        storedImage.IsCover.Should().BeFalse();
    }

    private static PortfolioCategory CreateCategory() => new()
    {
        Key = $"client-{Guid.NewGuid():N}",
        Name = "Client galleries",
        NameEn = "Client galleries"
    };

    private static PortfolioAlbum CreateUserUpload(PortfolioCategory category, DateTime expiresAtUtc) => new()
    {
        PortfolioCategory = category,
        Title = $"Upload {Guid.NewGuid():N}",
        Slug = $"upload-{Guid.NewGuid():N}",
        GalleryType = GalleryType.ClientPrintUpload,
        IsUserUploaded = true,
        ExpiresAtUtc = expiresAtUtc,
        UserGalleryStatus = UserClientGalleryStatus.Pending,
        AllowClientAccess = true,
        IsPublished = true
    };

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
