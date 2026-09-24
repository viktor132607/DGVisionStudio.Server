using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryAccessRefactorTests
{
    [Fact]
    public async Task QueryService_ReturnsOnlyActiveGalleryAccessesOrderedByEmail()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = TestUsers.Create("a@example.com");
        var second = TestUsers.Create("b@example.com");
        var category = Category();
        var activeAlbum = Album(category, "active");
        var deletedAlbum = Album(category, "deleted");
        deletedAlbum.IsDeleted = true;
        deletedAlbum.DeletedAtUtc = DateTime.UtcNow;

        fixture.Context.AddRange(first, second, category, activeAlbum, deletedAlbum);
        fixture.Context.UserAlbumAccesses.AddRange(
            Access(activeAlbum, second, preview: false, download: true),
            Access(activeAlbum, first, preview: true, download: false),
            Access(deletedAlbum, first, preview: true, download: true));
        await fixture.Context.SaveChangesAsync();

        var result = await new ClientGalleryAccessQueryService(fixture.Context)
            .GetGalleryAccessesAsync(activeAlbum.Id);

        result.Should().HaveCount(2);
        result.Select(x => x.Email).Should().Equal(
            "a@example.com",
            "b@example.com");
        result[0].PreviewEnabled.Should().BeTrue();
        result[1].DownloadEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GrantService_ReturnsFalseForMissingGalleryOrUser()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("known@example.com");
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryAccessGrantService(
            fixture.Context,
            new TestUserManager(user),
            NullLogger<ClientGalleryAccessGrantService>.Instance);

        (await service.GrantAccessAsync(
            999,
            new GrantGalleryAccessRequest
            {
                UserEmail = user.Email!
            })).Should().BeFalse();

        var category = Category();
        var album = Album(category, "gallery");
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        (await service.GrantAccessAsync(
            album.Id,
            new GrantGalleryAccessRequest
            {
                UserEmail = "missing@example.com"
            })).Should().BeFalse();
    }

    [Fact]
    public async Task MutationService_UpdatesThenRemovesAccess()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("user@example.com");
        var category = Category();
        var album = Album(category, "gallery");
        var access = Access(album, user, preview: true, download: false);
        fixture.Context.AddRange(user, category, album, access);
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryAccessMutationService(
            fixture.Context,
            NullLogger<ClientGalleryAccessMutationService>.Instance);
        var expiresAt = DateTime.UtcNow.AddDays(2);

        var updated = await service.UpdateAccessAsync(
            album.Id,
            user.Id,
            new UpdateGalleryAccessRequest
            {
                PreviewEnabled = false,
                DownloadEnabled = true,
                DownloadExpiresAtUtc = expiresAt
            });

        updated.Should().BeTrue();
        access.PreviewEnabled.Should().BeFalse();
        access.DownloadEnabled.Should().BeTrue();
        access.DownloadExpiresAtUtc.Should().Be(expiresAt);

        (await service.RemoveAccessAsync(album.Id, user.Id)).Should().BeTrue();
        (await fixture.Context.UserAlbumAccesses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SyncService_RemovesStaleAccessAndUpsertsEmailResolvedAccess()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var desiredUser = TestUsers.Create("desired@example.com");
        var staleUser = TestUsers.Create("stale@example.com");
        var category = Category();
        var album = Album(category, "gallery");
        var desiredAccess = Access(album, desiredUser, preview: false, download: false);
        var staleAccess = Access(album, staleUser, preview: true, download: true);

        fixture.Context.AddRange(
            desiredUser,
            staleUser,
            category,
            album,
            desiredAccess,
            staleAccess);
        await fixture.Context.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddDays(3);
        var service = new ClientGalleryAccessSyncService(
            fixture.Context,
            new TestUserManager(desiredUser));

        await service.SyncUserAccessesAsync(
            album.Id,
            [
                new GalleryUserAccessDto
                {
                    Email = "  desired@example.com  ",
                    PreviewEnabled = true,
                    DownloadEnabled = true,
                    DownloadExpiresAtUtc = expiresAt
                }
            ]);

        var stored = await fixture.Context.UserAlbumAccesses
            .AsNoTracking()
            .SingleAsync();

        stored.UserId.Should().Be(desiredUser.Id);
        stored.PreviewEnabled.Should().BeTrue();
        stored.DownloadEnabled.Should().BeTrue();
        stored.DownloadExpiresAtUtc.Should().Be(expiresAt);
    }

    [Fact]
    public async Task Facade_DelegatesAllFocusedAccessOperations()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("user@example.com");
        var category = Category();
        var album = Album(category, "gallery");
        fixture.Context.AddRange(user, category, album);
        await fixture.Context.SaveChangesAsync();

        var manager = new TestUserManager(user);
        var facade = new ClientGalleryAccessService(
            new ClientGalleryAccessQueryService(fixture.Context),
            new ClientGalleryAccessGrantService(
                fixture.Context,
                manager,
                NullLogger<ClientGalleryAccessGrantService>.Instance),
            new ClientGalleryAccessMutationService(
                fixture.Context,
                NullLogger<ClientGalleryAccessMutationService>.Instance),
            new ClientGalleryAccessSyncService(fixture.Context, manager));

        (await facade.GrantAccessAsync(
            album.Id,
            new GrantGalleryAccessRequest
            {
                UserEmail = user.Email!,
                PreviewEnabled = true
            })).Should().BeTrue();

        (await facade.GetGalleryAccessesAsync(album.Id))
            .Should().ContainSingle();

        (await facade.UpdateAccessAsync(
            album.Id,
            user.Id,
            new UpdateGalleryAccessRequest
            {
                PreviewEnabled = false,
                DownloadEnabled = true
            })).Should().BeTrue();

        await facade.SyncUserAccessesAsync(
            album.Id,
            [
                new GalleryUserAccessDto
                {
                    UserId = user.Id,
                    PreviewEnabled = true,
                    DownloadEnabled = false
                }
            ]);

        (await facade.RemoveAccessAsync(album.Id, user.Id))
            .Should().BeTrue();
    }

    private static PortfolioCategory Category() =>
        new()
        {
            Key = $"access-{Guid.NewGuid():N}",
            Name = "Access",
            NameEn = "Access",
            IsActive = true
        };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug) =>
        new()
        {
            PortfolioCategory = category,
            Title = slug,
            Slug = slug,
            AllowClientAccess = false
        };

    private static UserAlbumAccess Access(
        PortfolioAlbum album,
        ApplicationUser user,
        bool preview,
        bool download) =>
        new()
        {
            PortfolioAlbum = album,
            User = user,
            UserId = user.Id,
            PreviewEnabled = preview,
            DownloadEnabled = download
        };
}
