using System.Text.Json;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Coverage;

public sealed class HomeSlideshowImageAdditionalTests
{
    [Fact]
    public async Task GetManagementAsync_UsesSavedOrderFiltersUnavailableImagesAndMapsVideoAndTiming()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var active = Category("active", 2, active: true);
        var inactive = Category("inactive", 1, active: false);
        var visibleAlbum = Album(active, "visible", 2, published: true, userUploaded: false);
        var hiddenAlbum = Album(inactive, "hidden", 1, published: true, userUploaded: false);
        var first = Image(visibleAlbum, "/one.jpg", 2, published: true);
        var second = Image(visibleAlbum, "/two.jpg", 1, published: true);
        var unpublished = Image(visibleAlbum, "/draft.jpg", 3, published: false);
        var inactiveImage = Image(hiddenAlbum, "/inactive.jpg", 1, published: true);
        fixture.Context.AddRange(active, inactive, visibleAlbum, hiddenAlbum, first, second, unpublished, inactiveImage);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.SiteSettings.AddRange(
            new SiteSetting
            {
                Key = "home-slideshow-image-ids",
                Value = JsonSerializer.Serialize(new { imageIds = new[] { first.Id, 999999, second.Id }, intervalMs = 500 })
            },
            new SiteSetting
            {
                Key = "home-slideshow-intro-video-url",
                Value = "  /uploads/portfolio/intro.mp4  "
            });
        await fixture.Context.SaveChangesAsync();
        var settings = new HomeSlideshowSettingsService(fixture.Context);
        var service = new HomeSlideshowImageService(
            fixture.Context,
            settings,
            new HomeSlideshowVideoService(fixture.Context, new TestWebHostEnvironment()));

        var response = await service.GetManagementAsync();

        response.SelectedImages.Select(x => x.Id).Should().Equal(first.Id, second.Id);
        response.SelectedImages.Select(x => x.SlideshowOrder).Should().Equal(1, 2);
        response.AvailableImages.Select(x => x.Id).Should().Equal(second.Id, first.Id);
        response.AvailableImages.Should().OnlyContain(x => x.IsSelected);
        response.IntroVideoUrl.Should().Be("/uploads/portfolio/intro.mp4");
        response.UseDefaultInterval.Should().BeFalse();
        response.IntervalMs.Should().Be(1000);
    }

    [Fact]
    public async Task GetManagementAsync_SelectsAllAvailableImagesByDefaultAndMarksTheirOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var firstCategory = Category("first", 1, active: true);
        var secondCategory = Category("second", 2, active: true);
        var firstAlbum = Album(firstCategory, "first", 3, published: true, userUploaded: false);
        var secondAlbum = Album(secondCategory, "second", 1, published: true, userUploaded: false);
        var first = Image(firstAlbum, "/first.jpg", 2, published: true);
        var second = Image(firstAlbum, "/second.jpg", 1, published: true);
        var third = Image(secondAlbum, "/third.jpg", 1, published: true);
        fixture.Context.AddRange(firstCategory, secondCategory, firstAlbum, secondAlbum, first, second, third);
        await fixture.Context.SaveChangesAsync();
        var service = new HomeSlideshowImageService(
            fixture.Context,
            new HomeSlideshowSettingsService(fixture.Context),
            new HomeSlideshowVideoService(fixture.Context, new TestWebHostEnvironment()));

        var response = await service.GetManagementAsync();

        response.ImageIds.Should().Equal(second.Id, first.Id, third.Id);
        response.AvailableImages.Should().OnlyContain(x => x.IsSelected);
        response.AvailableImages.Select(x => x.SlideshowOrder).Should().Equal(1, 2, 3);
        response.UseDefaultInterval.Should().BeTrue();
        response.IntervalMs.Should().Be(4500);
    }

    [Fact]
    public async Task UpdateAsync_NormalizesRequestedIdsAndPersistsOnlyAvailableImages()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var active = Category("active", 1, active: true);
        var album = Album(active, "album", 1, published: true, userUploaded: false);
        var available = Image(album, "/available.jpg", 1, published: true);
        var unavailable = Image(album, "/unavailable.jpg", 2, published: false);
        fixture.Context.AddRange(active, album, available, unavailable);
        await fixture.Context.SaveChangesAsync();
        var settings = new HomeSlideshowSettingsService(fixture.Context);
        var service = new HomeSlideshowImageService(
            fixture.Context,
            settings,
            new HomeSlideshowVideoService(fixture.Context, new TestWebHostEnvironment()));

        await service.UpdateAsync(new UpdateHomeSlideshowRequest
        {
            ImageIds = [available.Id, available.Id, unavailable.Id, -1, 999999],
            UseDefaultInterval = false,
            IntervalMs = 50000
        });

        (await settings.GetSelectedImageIdsAsync()).Should().Equal(available.Id);
        var timing = await settings.GetResponseAsync();
        timing.UseDefaultInterval.Should().BeFalse();
        timing.IntervalMs.Should().Be(30000);
    }

    private static PortfolioCategory Category(string key, int order, bool active) => new()
    {
        Key = key,
        Name = key,
        NameEn = key,
        DisplayOrder = order,
        IsActive = active
    };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug,
        int order,
        bool published,
        bool userUploaded) => new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            DisplayOrder = order,
            IsPublished = published,
            IsUserUploaded = userUploaded,
            AllowClientAccess = true
        };

    private static PortfolioImage Image(
        PortfolioAlbum album,
        string url,
        int order,
        bool published) => new()
        {
            PortfolioAlbum = album,
            ImageUrl = url,
            DisplayOrder = order,
            IsPublished = published
        };
}

public sealed class ClientGalleryUserQueryAdditionalTests
{
    [Fact]
    public async Task GetMyGalleriesAsync_MergesAccessAndOwnedAlbumsWithoutDuplicatesAndSortsByLifetime()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("owner@example.com", "owner");
        var category = Category();
        var shared = Album(category, "shared", ownerId: null, expiresAtUtc: DateTime.UtcNow.AddDays(3));
        var owned = Album(category, "owned", ownerId: user.Id, expiresAtUtc: DateTime.UtcNow.AddDays(10));
        fixture.Context.AddRange(user, category, shared, owned);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserAlbumAccesses.AddRange(
            new UserAlbumAccess
            {
                UserId = user.Id,
                PortfolioAlbumId = shared.Id,
                PreviewEnabled = true,
                DownloadEnabled = true,
                DownloadExpiresAtUtc = DateTime.UtcNow.AddDays(2)
            },
            new UserAlbumAccess
            {
                UserId = user.Id,
                PortfolioAlbumId = owned.Id,
                PreviewEnabled = true,
                DownloadEnabled = true
            });
        await fixture.Context.SaveChangesAsync();
        var service = new ClientGalleryUserQueryService(fixture.Context, new ClientGalleryMapper());

        var result = await service.GetMyGalleriesAsync(user.Id);

        result.Select(x => x.Id).Should().Equal(owned.Id, shared.Id);
        result.Should().HaveCount(2);
        result.Single(x => x.Id == owned.Id).OwnerUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetGalleryDetailsAsync_HandlesMissingUnauthorizedOwnerAndGrantedAccessBranches()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var owner = TestUsers.Create("owner@example.com", "owner");
        var guest = TestUsers.Create("guest@example.com", "guest");
        var outsider = TestUsers.Create("outsider@example.com", "outsider");
        var category = Category();
        var ownedExpired = Album(category, "owned", owner.Id, DateTime.UtcNow.AddMinutes(-5));
        ownedExpired.Images.Add(new PortfolioImage
        {
            ImageUrl = "/published.jpg",
            IsPublished = true,
            DisplayOrder = 1
        });
        ownedExpired.Images.Add(new PortfolioImage
        {
            ImageUrl = "/draft.jpg",
            IsPublished = false,
            DisplayOrder = 2
        });
        var shared = Album(category, "shared", ownerId: null, expiresAtUtc: null);
        fixture.Context.AddRange(owner, guest, outsider, category, ownedExpired, shared);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserAlbumAccesses.Add(new UserAlbumAccess
        {
            UserId = guest.Id,
            PortfolioAlbumId = shared.Id,
            PreviewEnabled = true,
            DownloadEnabled = true,
            DownloadExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        });
        await fixture.Context.SaveChangesAsync();
        var service = new ClientGalleryUserQueryService(fixture.Context, new ClientGalleryMapper());

        var missing = await service.GetGalleryDetailsAsync(999999, owner.Id);
        var unauthorized = await service.GetGalleryDetailsAsync(shared.Id, outsider.Id);
        var ownerDetails = await service.GetGalleryDetailsAsync(ownedExpired.Id, owner.Id);
        var guestDetails = await service.GetGalleryDetailsAsync(shared.Id, guest.Id);

        missing.Should().BeNull();
        unauthorized.Should().BeNull();
        ownerDetails.Should().NotBeNull();
        ownerDetails!.DownloadEnabled.Should().BeFalse();
        ownerDetails.Photos.Should().ContainSingle(x => x.ImageUrl == "/published.jpg");
        guestDetails.Should().NotBeNull();
        guestDetails!.PreviewEnabled.Should().BeTrue();
        guestDetails.DownloadEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UserCanAccessGalleryAsync_HandlesOwnerPreviewExpiryAndAccessPermissions()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var owner = TestUsers.Create("owner@example.com", "owner");
        var guest = TestUsers.Create("guest@example.com", "guest");
        var category = Category();
        var expiredOwned = Album(category, "expired", owner.Id, DateTime.UtcNow.AddMinutes(-1));
        var shared = Album(category, "shared", ownerId: null, expiresAtUtc: null);
        fixture.Context.AddRange(owner, guest, category, expiredOwned, shared);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserAlbumAccesses.Add(new UserAlbumAccess
        {
            UserId = guest.Id,
            PortfolioAlbumId = shared.Id,
            PreviewEnabled = false,
            DownloadEnabled = true,
            DownloadExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await fixture.Context.SaveChangesAsync();
        var service = new ClientGalleryUserQueryService(fixture.Context, new ClientGalleryMapper());

        (await service.UserCanAccessGalleryAsync(999999, owner.Id, false)).Should().BeFalse();
        (await service.UserCanAccessGalleryAsync(expiredOwned.Id, owner.Id, false)).Should().BeTrue();
        (await service.UserCanAccessGalleryAsync(expiredOwned.Id, owner.Id, true)).Should().BeFalse();
        (await service.UserCanAccessGalleryAsync(shared.Id, guest.Id, false)).Should().BeFalse();
        (await service.UserCanAccessGalleryAsync(shared.Id, guest.Id, true)).Should().BeFalse();
        (await service.UserCanAccessGalleryAsync(shared.Id, "missing-user", false)).Should().BeFalse();
    }

    private static PortfolioCategory Category() => new()
    {
        Key = "client-galleries",
        Name = "Client galleries",
        NameEn = "Client galleries",
        IsActive = true
    };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug,
        string? ownerId,
        DateTime? expiresAtUtc) => new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            GalleryType = ownerId == null ? default : GalleryType.ClientPrintUpload,
            IsUserUploaded = ownerId != null,
            OwnerUserId = ownerId,
            ExpiresAtUtc = expiresAtUtc,
            AllowClientAccess = true,
            IsPublished = true
        };
}
