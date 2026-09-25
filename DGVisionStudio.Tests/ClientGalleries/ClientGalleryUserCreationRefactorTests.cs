using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryUserCreationRefactorTests
{
    [Fact]
    public async Task AlbumCreationService_RejectsEleventhActiveUserGallery()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("user@test.bg", "user-1");
        var category = new PortfolioCategory
        {
            Key = "client-galleries",
            Name = "Client Galleries",
            NameEn = "Client Galleries",
            IsActive = false
        };
        fixture.Context.AddRange(user, category);
        await fixture.Context.SaveChangesAsync();

        for (var index = 1; index <= 10; index++)
        {
            fixture.Context.PortfolioAlbums.Add(new PortfolioAlbum
            {
                PortfolioCategoryId = category.Id,
                Slug = $"upload-{index}",
                Title = $"Upload {index}",
                GalleryType = GalleryType.ClientPrintUpload,
                IsUserUploaded = true,
                OwnerUserId = user.Id,
                AllowClientAccess = true,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                IsDeleted = false
            });
        }

        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryUserAlbumCreationService(
            fixture.Context,
            new ClientGalleryNamingService(fixture.Context),
            NullLogger<ClientGalleryUserCreationService>.Instance);

        var result = await service.CreateAsync(
            user.Id,
            new CreateUserClientGalleryRequest { Title = "Eleventh" });

        result.Should().BeNull();
        (await fixture.Context.PortfolioAlbums.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task AlbumCreationService_PreservesNormalizationLifetimeAndDisplayOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("user@test.bg", "user-1");
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryUserAlbumCreationService(
            fixture.Context,
            new ClientGalleryNamingService(fixture.Context),
            NullLogger<ClientGalleryUserCreationService>.Instance);
        var before = DateTime.UtcNow;

        var id = await service.CreateAsync(
            user.Id,
            new CreateUserClientGalleryRequest
            {
                Title = "  My Upload  ",
                Description = "  Prints  "
            });

        id.Should().NotBeNull();
        var album = await fixture.Context.PortfolioAlbums.SingleAsync();
        album.Title.Should().Be("My Upload");
        album.Description.Should().Be("Prints");
        album.DisplayOrder.Should().Be(1);
        album.GalleryType.Should().Be(GalleryType.ClientPrintUpload);
        album.UserGalleryStatus.Should().Be(UserClientGalleryStatus.Pending);
        album.ExpiresAtUtc.Should().BeAfter(before.AddDays(6));
    }

    [Fact]
    public async Task PhotoUploadService_PersistsFirstPhotoAndSetsGalleryCover()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("user@test.bg", "user-1");
        var category = new PortfolioCategory
        {
            Key = "client-galleries",
            Name = "Client Galleries",
            NameEn = "Client Galleries"
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "upload",
            Title = "Upload",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            OwnerUserId = user.Id,
            AllowClientAccess = true,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(2)
        };
        fixture.Context.AddRange(user, category, album);
        await fixture.Context.SaveChangesAsync();

        var storage = new StubFileStorageService
        {
            SavedPath = "/uploads/client-galleries/originals/saved.jpg"
        };
        var service = new ClientGalleryUserPhotoUploadService(
            fixture.Context,
            storage,
            new ClientGalleryMapper(),
            new ClientGalleryUploadValidator(),
            NullLogger<ClientGalleryUserCreationService>.Instance);

        var result = await service.UploadAsync(
            album.Id,
            user.Id,
            GalleryTestFiles.Create(
                "  portrait.jpg",
                "image/jpeg",
                [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]));

        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        var photo = await fixture.Context.PortfolioImages.SingleAsync();
        photo.ImageUrl.Should().Be(storage.SavedPath);
        photo.ThumbnailUrl.Should().Be(storage.SavedPath);
        photo.AltText.Should().Be("portrait");
        photo.DisplayOrder.Should().Be(1);
        photo.IsCover.Should().BeTrue();

        var storedAlbum = await fixture.Context.PortfolioAlbums.SingleAsync();
        storedAlbum.CoverImageUrl.Should().Be(storage.SavedPath);
        storage.Files.Should().ContainKey(storage.SavedPath);
    }

    [Fact]
    public async Task PhotoUploadService_RejectsExpiredGalleryWithoutSavingFile()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("user@test.bg", "user-1");
        var category = new PortfolioCategory
        {
            Key = "client-galleries",
            Name = "Client Galleries",
            NameEn = "Client Galleries"
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "expired",
            Title = "Expired",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            OwnerUserId = user.Id,
            AllowClientAccess = true,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        fixture.Context.AddRange(user, category, album);
        await fixture.Context.SaveChangesAsync();

        var storage = new StubFileStorageService();
        var service = new ClientGalleryUserPhotoUploadService(
            fixture.Context,
            storage,
            new ClientGalleryMapper(),
            new ClientGalleryUploadValidator(),
            NullLogger<ClientGalleryUserCreationService>.Instance);

        var result = await service.UploadAsync(
            album.Id,
            user.Id,
            GalleryTestFiles.Create(
                "photo.jpg",
                "image/jpeg",
                [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]));

        result.Should().BeNull();
        storage.Files.Should().BeEmpty();
        (await fixture.Context.PortfolioImages.CountAsync()).Should().Be(0);
    }
}
