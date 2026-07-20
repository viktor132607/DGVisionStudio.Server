using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryPhotoDownloadServiceTests
{
    [Fact]
    public async Task OpenPhotoDownloadAsync_ReturnsAdminDownloadWithResolvedContentTypeAndName()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory { Key = "client", Name = "Client", NameEn = "Client" };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Title = "My Gallery",
            Slug = "my-gallery",
            AllowClientAccess = true
        };
        var photo = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/uploads/photo.png",
            IsPublished = true
        };
        db.AddRange(category, album, photo);
        await db.SaveChangesAsync();
        var storage = new StubFileStorageService();
        storage.Files[photo.ImageUrl] = [1, 2, 3];
        var service = new ClientGalleryPhotoDownloadService(
            db,
            storage,
            new ClientGalleryMapper(),
            new ClientGalleryNamingService(db),
            NullLogger<ClientGalleryPhotoDownloadService>.Instance);

        var result = await service.OpenPhotoDownloadAsync(album.Id, photo.Id, "", isAdmin: true);

        result.Should().NotBeNull();
        result!.Value.ContentType.Should().Be("image/png");
        result.Value.FileName.Should().Be($"my-gallery-{photo.Id}.png");
        await result.Value.Stream.DisposeAsync();
    }

    [Fact]
    public async Task OpenPhotoDownloadAsync_ReturnsNull_WhenAccessIsDenied()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory { Key = "client", Name = "Client", NameEn = "Client" };
        var album = new PortfolioAlbum { PortfolioCategory = category, Title = "Private", Slug = "private" };
        var photo = new PortfolioImage { PortfolioAlbum = album, ImageUrl = "/uploads/private.jpg" };
        db.AddRange(category, album, photo);
        await db.SaveChangesAsync();
        var service = new ClientGalleryPhotoDownloadService(
            db,
            new StubFileStorageService(),
            new ClientGalleryMapper(),
            new ClientGalleryNamingService(db),
            NullLogger<ClientGalleryPhotoDownloadService>.Instance);

        var result = await service.OpenPhotoDownloadAsync(album.Id, photo.Id, "unknown", isAdmin: false);

        result.Should().BeNull();
    }
}

public sealed class ClientGalleryPhotoUploadServiceTests
{
    [Fact]
    public async Task UploadPhotoAsync_ReturnsNull_WhenGalleryDoesNotExist()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new ClientGalleryPhotoUploadService(
            fixture.Context,
            new StubFileStorageService(),
            new ClientGalleryMapper(),
            new ClientGalleryUploadValidator(),
            NullLogger<ClientGalleryPhotoUploadService>.Instance);

        var result = await service.UploadPhotoAsync(
            999,
            GalleryTestFiles.Create("valid.png", "image/png"));

        result.Should().BeNull();
        (await fixture.Context.PortfolioImages.CountAsync()).Should().Be(0);
    }
}

public sealed class ClientGalleryPhotoMutationServiceTests
{
    [Fact]
    public async Task MutationMethods_ReturnFalseOrNull_WhenGalleryDoesNotExist()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new ClientGalleryPhotoMutationService(
            fixture.Context,
            new ClientGalleryMapper(),
            NullLogger<ClientGalleryPhotoMutationService>.Instance);

        (await service.UpdatePhotoAsync(1, 1, new DGVisionStudio.Application.DTOs.ClientGalleries.UpdateClientPhotoRequest()))
            .Should().BeNull();
        (await service.DeletePhotoAsync(1, 1)).Should().BeFalse();
        (await service.SetCoverImageAsync(1, "/missing.jpg")).Should().BeFalse();
        (await service.ReorderPhotosAsync(1, [1, 2])).Should().BeFalse();
    }

    [Fact]
    public async Task ReorderPhotosAsync_AssignsRequestedOrderAndAppendsRemainingPhotos()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory { Key = "photos", Name = "Photos", NameEn = "Photos" };
        var album = new PortfolioAlbum { PortfolioCategory = category, Title = "Album", Slug = "album" };
        var first = new PortfolioImage { PortfolioAlbum = album, ImageUrl = "/1.jpg", DisplayOrder = 1 };
        var second = new PortfolioImage { PortfolioAlbum = album, ImageUrl = "/2.jpg", DisplayOrder = 2 };
        var third = new PortfolioImage { PortfolioAlbum = album, ImageUrl = "/3.jpg", DisplayOrder = 3 };
        fixture.Context.AddRange(category, album, first, second, third);
        await fixture.Context.SaveChangesAsync();
        var service = new ClientGalleryPhotoMutationService(
            fixture.Context,
            new ClientGalleryMapper(),
            NullLogger<ClientGalleryPhotoMutationService>.Instance);

        var updated = await service.ReorderPhotosAsync(album.Id, [third.Id, first.Id]);

        updated.Should().BeTrue();
        var stored = await fixture.Context.PortfolioImages.OrderBy(x => x.DisplayOrder).ToListAsync();
        stored.Select(x => x.Id).Should().Equal(third.Id, first.Id, second.Id);
    }
}

public sealed class ClientGalleryPhotoServiceTests
{
    [Fact]
    public async Task DeletePhotoAsync_DelegatesToMutationService()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var storage = new StubFileStorageService();
        var mapper = new ClientGalleryMapper();
        var naming = new ClientGalleryNamingService(fixture.Context);
        var downloads = new ClientGalleryPhotoDownloadService(
            fixture.Context,
            storage,
            mapper,
            naming,
            NullLogger<ClientGalleryPhotoDownloadService>.Instance);
        var uploads = new ClientGalleryPhotoUploadService(
            fixture.Context,
            storage,
            mapper,
            new ClientGalleryUploadValidator(),
            NullLogger<ClientGalleryPhotoUploadService>.Instance);
        var mutations = new ClientGalleryPhotoMutationService(
            fixture.Context,
            mapper,
            NullLogger<ClientGalleryPhotoMutationService>.Instance);
        var service = new ClientGalleryPhotoService(downloads, uploads, mutations);

        var deleted = await service.DeletePhotoAsync(999, 999);

        deleted.Should().BeFalse();
    }
}
