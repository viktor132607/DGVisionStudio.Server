using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryUserCreationServiceTests
{
    [Fact]
    public async Task CreateUserGalleryAsync_ReturnsNull_ForBlankTitle()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = Create(fixture.Context);

        var result = await service.CreateUserGalleryAsync(
            "user-1",
            new CreateUserClientGalleryRequest { Title = "   " });

        result.Should().BeNull();
        (await fixture.Context.PortfolioAlbums.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateUserGalleryAsync_CreatesSevenDayPrivatePrintUpload()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        fixture.Context.Users.Add(TestUsers.Create("user@test.bg", "user-1"));
        await fixture.Context.SaveChangesAsync();
        var service = Create(fixture.Context);
        var before = DateTime.UtcNow;

        var id = await service.CreateUserGalleryAsync(
            "user-1",
            new CreateUserClientGalleryRequest
            {
                Title = "  My Upload  ",
                Description = "  Prints  "
            });

        id.Should().NotBeNull();
        var album = await fixture.Context.PortfolioAlbums.SingleAsync();
        album.Title.Should().Be("My Upload");
        album.Description.Should().Be("Prints");
        album.OwnerUserId.Should().Be("user-1");
        album.GalleryType.Should().Be(GalleryType.ClientPrintUpload);
        album.IsUserUploaded.Should().BeTrue();
        album.IsPublished.Should().BeFalse();
        album.ExpiresAtUtc.Should().BeAfter(before.AddDays(6));
    }

    private static ClientGalleryUserCreationService Create(DGVisionStudio.Infrastructure.Data.AppDbContext db) =>
        new(
            db,
            new StubFileStorageService(),
            new ClientGalleryMapper(),
            new ClientGalleryUploadValidator(),
            new ClientGalleryNamingService(db),
            NullLogger<ClientGalleryUserCreationService>.Instance);
}

public sealed class ClientGalleryUserQueryServiceTests
{
    [Fact]
    public async Task UserCanAccessGalleryAsync_AllowsActiveOwnerDownloadAndRejectsExpiredOwnerDownload()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory { Key = "client-galleries", Name = "Client", NameEn = "Client" };
        var active = CreateOwned(category, "active", DateTime.UtcNow.AddHours(1));
        var expired = CreateOwned(category, "expired", DateTime.UtcNow.AddHours(-1));
        db.AddRange(category, active, expired);
        await db.SaveChangesAsync();
        var service = new ClientGalleryUserQueryService(db, new ClientGalleryMapper());

        (await service.UserCanAccessGalleryAsync(active.Id, "owner", requireDownload: true)).Should().BeTrue();
        (await service.UserCanAccessGalleryAsync(expired.Id, "owner", requireDownload: true)).Should().BeFalse();
        (await service.UserCanAccessGalleryAsync(active.Id, "other", requireDownload: false)).Should().BeFalse();
    }

    [Fact]
    public async Task GetMyGalleriesAsync_IncludesOwnedGalleryOnlyOnce()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory { Key = "client-galleries", Name = "Client", NameEn = "Client" };
        var album = CreateOwned(category, "owned", DateTime.UtcNow.AddDays(2));
        db.AddRange(category, album);
        await db.SaveChangesAsync();
        var service = new ClientGalleryUserQueryService(db, new ClientGalleryMapper());

        var result = await service.GetMyGalleriesAsync("owner");

        result.Should().ContainSingle(x => x.Id == album.Id && x.DownloadEnabled);
    }

    private static PortfolioAlbum CreateOwned(PortfolioCategory category, string slug, DateTime expires) => new()
    {
        PortfolioCategory = category,
        Title = slug,
        Slug = slug,
        GalleryType = GalleryType.ClientPrintUpload,
        IsUserUploaded = true,
        OwnerUserId = "owner",
        ExpiresAtUtc = expires,
        AllowClientAccess = true
    };
}

public sealed class ClientGalleryUserLifecycleServiceTests
{
    [Fact]
    public async Task DeleteUserGalleryAsync_SoftDeletesAlbumAndPhotosAndDeletesStoredFiles()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory { Key = "client-galleries", Name = "Client", NameEn = "Client" };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Title = "Upload",
            Slug = "upload",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            OwnerUserId = "owner",
            AllowClientAccess = true,
            IsPublished = true
        };
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/uploads/client-galleries/originals/a.png",
            IsPublished = true
        });
        db.AddRange(category, album);
        await db.SaveChangesAsync();
        var storage = new StubFileStorageService();
        storage.Files[album.Images.Single().ImageUrl] = [1];
        var service = new ClientGalleryUserLifecycleService(
            db,
            storage,
            NullLogger<ClientGalleryUserLifecycleService>.Instance);

        var deleted = await service.DeleteUserGalleryAsync(album.Id, "owner");

        deleted.Should().BeTrue();
        var storedAlbum = await db.PortfolioAlbums.IgnoreQueryFilters().SingleAsync();
        storedAlbum.IsDeleted.Should().BeTrue();
        storedAlbum.AllowClientAccess.Should().BeFalse();
        var storedPhoto = await db.PortfolioImages.IgnoreQueryFilters().SingleAsync();
        storedPhoto.IsDeleted.Should().BeTrue();
        storage.DeletedPaths.Should().ContainSingle(album.Images.Single().ImageUrl);
    }
}

public sealed class ClientGalleryUserServiceTests
{
    [Fact]
    public async Task GetMyGalleriesAsync_DelegatesToQueryService()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var mapper = new ClientGalleryMapper();
        var storage = new StubFileStorageService();
        var naming = new ClientGalleryNamingService(fixture.Context);
        var service = new ClientGalleryUserService(
            new ClientGalleryUserQueryService(fixture.Context, mapper),
            new ClientGalleryUserCreationService(
                fixture.Context,
                storage,
                mapper,
                new ClientGalleryUploadValidator(),
                naming,
                NullLogger<ClientGalleryUserCreationService>.Instance),
            new ClientGalleryUserLifecycleService(
                fixture.Context,
                storage,
                NullLogger<ClientGalleryUserLifecycleService>.Instance));

        var result = await service.GetMyGalleriesAsync("user");

        result.Should().BeEmpty();
    }
}
