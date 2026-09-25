using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;

namespace DGVisionStudio.Tests.Slideshow;

public sealed class HomeSlideshowImageRefactorTests
{
    [Fact]
    public void Mapper_PreservesImageAlbumCategoryAndSelectionFields()
    {
        var category = new PortfolioCategory
        {
            Name = "Weddings",
            NameEn = "Weddings EN"
        };
        var album = new PortfolioAlbum
        {
            Id = 4,
            Title = "Album",
            PortfolioCategory = category
        };
        var image = new PortfolioImage
        {
            Id = 9,
            PortfolioAlbumId = album.Id,
            PortfolioAlbum = album,
            ImageUrl = "/photo.jpg",
            ThumbnailUrl = "/thumb.jpg",
            AltText = "Alt",
            Caption = "Caption",
            DisplayOrder = 3,
            IsPublished = true
        };

        var dto = new HomeSlideshowImageMapper()
            .Map(
                image,
                isSelected: true,
                slideshowOrder: 2);

        dto.Id.Should().Be(9);
        dto.ImageUrl.Should().Be("/photo.jpg");
        dto.ThumbnailUrl.Should().Be("/thumb.jpg");
        dto.AlbumTitle.Should().Be("Album");
        dto.CategoryName.Should().Be("Weddings");
        dto.CategoryNameEn.Should().Be("Weddings EN");
        dto.IsSelected.Should().BeTrue();
        dto.SlideshowOrder.Should().Be(2);
    }

    [Fact]
    public async Task Catalog_PreservesVisibilityScheduleAndDefaultOrder()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var firstCategory = Category(
            "first",
            order: 1);
        var secondCategory = Category(
            "second",
            order: 2);

        var scheduledAlbum = Album(
            firstCategory,
            "scheduled",
            order: 1,
            publishAtUtc:
                DateTime.UtcNow.AddDays(1));

        var visibleAlbum = Album(
            secondCategory,
            "visible",
            order: 1);

        var scheduled = Image(
            scheduledAlbum,
            "/scheduled.jpg",
            order: 1);

        var second = Image(
            visibleAlbum,
            "/second.jpg",
            order: 2);

        var first = Image(
            visibleAlbum,
            "/first.jpg",
            order: 1);

        var unpublished = Image(
            visibleAlbum,
            "/hidden.jpg",
            order: 3);
        unpublished.IsPublished = false;

        context.AddRange(
            firstCategory,
            secondCategory,
            scheduledAlbum,
            visibleAlbum,
            scheduled,
            second,
            first,
            unpublished);

        await context.SaveChangesAsync();

        var catalog =
            new HomeSlideshowImageCatalogService(
                context);

        var publicImages =
            await catalog.GetAvailableAsync();

        var managementImages =
            await catalog.GetAvailableAsync(
                includeScheduled: true);

        publicImages
            .Select(image => image.ImageUrl)
            .Should()
            .Equal(
                "/first.jpg",
                "/second.jpg");

        managementImages
            .Select(image => image.ImageUrl)
            .Should()
            .Equal(
                "/scheduled.jpg",
                "/first.jpg",
                "/second.jpg");
    }

    [Fact]
    public async Task SelectionService_PreservesSavedOrderAndMissingFiltering()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var category =
            Category("category", 1);
        var album =
            Album(category, "album", 1);
        var first =
            Image(album, "/first.jpg", 1);
        var second =
            Image(album, "/second.jpg", 2);

        context.AddRange(
            category,
            album,
            first,
            second);
        await context.SaveChangesAsync();

        var settings =
            new HomeSlideshowSettingsService(
                context);

        await settings.UpdateAsync(
            new UpdateHomeSlideshowRequest
            {
                ImageIds =
                    [second.Id, 999999, first.Id]
            },
            availableIds:
                [first.Id, second.Id]);

        var service =
            new HomeSlideshowImageSelectionService(
                new HomeSlideshowImageCatalogService(
                    context),
                settings,
                new HomeSlideshowImageMapper());

        var result =
            await service.GetSlideshowImagesAsync();

        result.Select(image => image.Id)
            .Should()
            .Equal(second.Id, first.Id);

        result.Select(image => image.SlideshowOrder)
            .Should()
            .Equal(1, 2);

        result.Should()
            .OnlyContain(image => image.IsSelected);
    }

    [Fact]
    public async Task CompatibilityConstructor_PreservesEmptyFacadeBehavior()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var settings =
            new HomeSlideshowSettingsService(
                context);
        var video =
            new HomeSlideshowVideoService(
                context,
                new TestWebHostEnvironment());

        var service =
            new HomeSlideshowImageService(
                context,
                settings,
                video);

        var result =
            await service.GetSlideshowImagesAsync();

        result.Should().BeEmpty();
    }

    private static PortfolioCategory Category(
        string key,
        int order) =>
        new()
        {
            Key = key,
            Name = key,
            NameEn = key,
            DisplayOrder = order,
            IsActive = true
        };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug,
        int order,
        DateTime? publishAtUtc = null) =>
        new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            DisplayOrder = order,
            IsPublished = true,
            IsUserUploaded = false,
            PublishAtUtc = publishAtUtc
        };

    private static PortfolioImage Image(
        PortfolioAlbum album,
        string url,
        int order) =>
        new()
        {
            PortfolioAlbum = album,
            ImageUrl = url,
            DisplayOrder = order,
            IsPublished = true
        };
}
