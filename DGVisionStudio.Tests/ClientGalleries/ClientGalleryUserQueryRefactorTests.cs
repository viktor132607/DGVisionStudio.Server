using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryUserQueryRefactorTests
{
    [Fact]
    public async Task ListQuery_PreservesMergeDeduplicationAndOrdering()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var user =
            TestUsers.Create(
                "owner@example.com",
                "owner");

        var category = Category();

        var shared = Album(
            category,
            "shared",
            ownerId: null,
            expiresAtUtc:
                DateTime.UtcNow.AddDays(3));

        var owned = Album(
            category,
            "owned",
            ownerId: user.Id,
            expiresAtUtc:
                DateTime.UtcNow.AddDays(10));

        fixture.Context.AddRange(
            user,
            category,
            shared,
            owned);

        await fixture.Context.SaveChangesAsync();

        fixture.Context.UserAlbumAccesses.AddRange(
            Access(user, shared),
            Access(user, owned));

        await fixture.Context.SaveChangesAsync();

        var service =
            new ClientGalleryUserListQueryService(
                fixture.Context,
                new ClientGalleryMapper());

        var result =
            await service.GetMyGalleriesAsync(
                user.Id);

        result.Should().HaveCount(2);
        result.Select(item => item.Id)
            .Should()
            .Equal(owned.Id, shared.Id);
    }

    [Fact]
    public async Task DetailsQuery_PreservesOwnerAndUnauthorizedRules()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var owner =
            TestUsers.Create(
                "owner@example.com",
                "owner");

        var outsider =
            TestUsers.Create(
                "outsider@example.com",
                "outsider");

        var category = Category();
        var album = Album(
            category,
            "owned",
            owner.Id,
            DateTime.UtcNow.AddHours(1));

        album.Images.Add(
            new PortfolioImage
            {
                ImageUrl = "/photo.jpg",
                IsPublished = true
            });

        fixture.Context.AddRange(
            owner,
            outsider,
            category,
            album);

        await fixture.Context.SaveChangesAsync();

        var service =
            new ClientGalleryUserDetailsQueryService(
                fixture.Context,
                new ClientGalleryMapper());

        var ownerResult =
            await service.GetGalleryDetailsAsync(
                album.Id,
                owner.Id);

        var outsiderResult =
            await service.GetGalleryDetailsAsync(
                album.Id,
                outsider.Id);

        ownerResult.Should().NotBeNull();
        ownerResult!.DownloadEnabled.Should().BeTrue();
        outsiderResult.Should().BeNull();
    }

    [Fact]
    public async Task AccessQuery_PreservesPreviewDownloadAndOwnerExpiryPolicy()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var owner =
            TestUsers.Create(
                "owner@example.com",
                "owner");

        var guest =
            TestUsers.Create(
                "guest@example.com",
                "guest");

        var category = Category();

        var expiredOwned = Album(
            category,
            "expired",
            owner.Id,
            DateTime.UtcNow.AddMinutes(-1));

        var shared = Album(
            category,
            "shared",
            ownerId: null,
            expiresAtUtc: null);

        fixture.Context.AddRange(
            owner,
            guest,
            category,
            expiredOwned,
            shared);

        await fixture.Context.SaveChangesAsync();

        fixture.Context.UserAlbumAccesses.Add(
            new UserAlbumAccess
            {
                UserId = guest.Id,
                PortfolioAlbumId = shared.Id,
                PreviewEnabled = true,
                DownloadEnabled = true,
                DownloadExpiresAtUtc =
                    DateTime.UtcNow.AddMinutes(-1)
            });

        await fixture.Context.SaveChangesAsync();

        var service =
            new ClientGalleryUserAccessQueryService(
                fixture.Context,
                new ClientGalleryMapper());

        (await service.UserCanAccessGalleryAsync(
                expiredOwned.Id,
                owner.Id,
                requireDownload: false))
            .Should().BeTrue();

        (await service.UserCanAccessGalleryAsync(
                expiredOwned.Id,
                owner.Id,
                requireDownload: true))
            .Should().BeFalse();

        (await service.UserCanAccessGalleryAsync(
                shared.Id,
                guest.Id,
                requireDownload: false))
            .Should().BeTrue();

        (await service.UserCanAccessGalleryAsync(
                shared.Id,
                guest.Id,
                requireDownload: true))
            .Should().BeFalse();
    }

    [Fact]
    public async Task FacadeCompatibilityConstructor_PreservesQueryBehavior()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var service =
            new ClientGalleryUserQueryService(
                fixture.Context,
                new ClientGalleryMapper());

        var result =
            await service.GetMyGalleriesAsync(
                "missing-user");

        result.Should().BeEmpty();
    }

    private static PortfolioCategory Category() =>
        new()
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
        DateTime? expiresAtUtc) =>
        new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            GalleryType = ownerId == null
                ? default
                : GalleryType.ClientPrintUpload,
            IsUserUploaded = ownerId != null,
            OwnerUserId = ownerId,
            ExpiresAtUtc = expiresAtUtc,
            AllowClientAccess = true,
            IsPublished = true
        };

    private static UserAlbumAccess Access(
        ApplicationUser user,
        PortfolioAlbum album) =>
        new()
        {
            UserId = user.Id,
            PortfolioAlbumId = album.Id,
            PreviewEnabled = true,
            DownloadEnabled = true
        };
}
