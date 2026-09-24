using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryPhotoMutationRefactorTests
{
    [Theory]
    [InlineData("/uploads/photo.jpg", "uploads/photo.jpg")]
    [InlineData("\\uploads\\photo.jpg", "uploads/photo.jpg")]
    [InlineData("https://cdn.example.com/uploads/photo.jpg", "uploads/photo.jpg")]
    public void CoverService_NormalizesStoredPaths(
        string input,
        string expected)
    {
        ClientGalleryPhotoCoverService
            .NormalizeStoredImagePath(input)
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task UpdateService_NormalizesMetadataAndSetsCover()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var category = Category();
        var album = Album(category);
        var first = Photo("/one.jpg", 1);
        var second = Photo("/two.jpg", 2);
        album.Images.Add(first);
        album.Images.Add(second);

        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var coverService = new ClientGalleryPhotoCoverService(
            fixture.Context,
            NullLogger<ClientGalleryPhotoCoverService>.Instance);

        var service = new ClientGalleryPhotoUpdateService(
            fixture.Context,
            new ClientGalleryMapper(),
            coverService,
            NullLogger<ClientGalleryPhotoUpdateService>.Instance);

        var result = await service.UpdatePhotoAsync(
            album.Id,
            second.Id,
            new UpdateClientPhotoRequest
            {
                AltText = "  alt  ",
                Caption = " ",
                Description = "  description  ",
                DisplayOrder = 5,
                IsPublished = false,
                IsCover = true
            });

        result.Should().NotBeNull();
        second.AltText.Should().Be("alt");
        second.Caption.Should().Be("description");
        second.DisplayOrder.Should().Be(5);
        second.IsPublished.Should().BeFalse();
        second.IsCover.Should().BeTrue();
        first.IsCover.Should().BeFalse();
        album.CoverImageUrl.Should().Be(second.ImageUrl);
    }

    [Fact]
    public async Task DeleteService_SoftDeletesCoverAndSelectsFirstFallback()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var category = Category();
        var album = Album(category);
        var cover = Photo("/cover.jpg", 1);
        cover.IsCover = true;
        var fallback = Photo("/fallback.jpg", 2);
        album.Images.Add(cover);
        album.Images.Add(fallback);
        album.CoverImageUrl = cover.ImageUrl;

        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var coverService = new ClientGalleryPhotoCoverService(
            fixture.Context,
            NullLogger<ClientGalleryPhotoCoverService>.Instance);

        var service = new ClientGalleryPhotoDeleteService(
            fixture.Context,
            coverService,
            NullLogger<ClientGalleryPhotoDeleteService>.Instance);

        var deleted = await service.DeletePhotoAsync(
            album.Id,
            cover.Id);

        deleted.Should().BeTrue();
        cover.IsDeleted.Should().BeTrue();
        cover.IsPublished.Should().BeFalse();
        cover.IsCover.Should().BeFalse();
        cover.DeletedAtUtc.Should().NotBeNull();

        fallback.IsCover.Should().BeTrue();
        album.CoverImageUrl.Should().Be(
            fallback.ImageUrl);
    }

    [Fact]
    public async Task CoverService_MatchesAbsoluteUrlAgainstStoredPath()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var category = Category();
        var album = Album(category);
        var photo = Photo(
            "/uploads/photo.jpg",
            1);
        album.Images.Add(photo);

        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryPhotoCoverService(
            fixture.Context,
            NullLogger<ClientGalleryPhotoCoverService>.Instance);

        var changed = await service.SetCoverImageAsync(
            album.Id,
            "https://cdn.example.com/uploads/photo.jpg");

        changed.Should().BeTrue();
        photo.IsCover.Should().BeTrue();
        album.CoverImageUrl.Should().Be(photo.ImageUrl);
    }

    [Fact]
    public async Task ReorderService_AppendsUnspecifiedPhotosInExistingOrder()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var category = Category();
        var album = Album(category);
        var first = Photo("/1.jpg", 1);
        var second = Photo("/2.jpg", 2);
        var third = Photo("/3.jpg", 3);
        album.Images.Add(first);
        album.Images.Add(second);
        album.Images.Add(third);

        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryPhotoReorderService(
            fixture.Context,
            NullLogger<ClientGalleryPhotoReorderService>.Instance);

        var reordered = await service.ReorderPhotosAsync(
            album.Id,
            [third.Id, first.Id]);

        reordered.Should().BeTrue();

        var stored = await fixture.Context.PortfolioImages
            .OrderBy(x => x.DisplayOrder)
            .Select(x => x.Id)
            .ToListAsync();

        stored.Should().Equal(
            third.Id,
            first.Id,
            second.Id);
    }

    [Fact]
    public async Task Facade_DelegatesFocusedMutationOperations()
    {
        await using var fixture =
            await GallerySqliteFixture.CreateAsync();

        var category = Category();
        var album = Album(category);
        var photo = Photo("/photo.jpg", 1);
        album.Images.Add(photo);

        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var covers = new ClientGalleryPhotoCoverService(
            fixture.Context,
            NullLogger<ClientGalleryPhotoCoverService>.Instance);

        var facade = new ClientGalleryPhotoMutationService(
            new ClientGalleryPhotoUpdateService(
                fixture.Context,
                new ClientGalleryMapper(),
                covers,
                NullLogger<ClientGalleryPhotoUpdateService>.Instance),
            new ClientGalleryPhotoDeleteService(
                fixture.Context,
                covers,
                NullLogger<ClientGalleryPhotoDeleteService>.Instance),
            covers,
            new ClientGalleryPhotoReorderService(
                fixture.Context,
                NullLogger<ClientGalleryPhotoReorderService>.Instance));

        (await facade.SetCoverImageAsync(
            album.Id,
            photo.ImageUrl))
            .Should()
            .BeTrue();

        (await facade.UpdatePhotoAsync(
            album.Id,
            photo.Id,
            new UpdateClientPhotoRequest
            {
                Caption = "updated"
            }))
            .Should()
            .NotBeNull();

        (await facade.ReorderPhotosAsync(
            album.Id,
            [photo.Id]))
            .Should()
            .BeTrue();

        (await facade.DeletePhotoAsync(
            album.Id,
            photo.Id))
            .Should()
            .BeTrue();
    }

    private static PortfolioCategory Category() =>
        new()
        {
            Key = $"photos-{Guid.NewGuid():N}",
            Name = "Photos",
            NameEn = "Photos",
            IsActive = true
        };

    private static PortfolioAlbum Album(
        PortfolioCategory category) =>
        new()
        {
            PortfolioCategory = category,
            Title = "Album",
            Slug = $"album-{Guid.NewGuid():N}"
        };

    private static PortfolioImage Photo(
        string imageUrl,
        int order) =>
        new()
        {
            ImageUrl = imageUrl,
            DisplayOrder = order,
            IsPublished = true
        };
}
