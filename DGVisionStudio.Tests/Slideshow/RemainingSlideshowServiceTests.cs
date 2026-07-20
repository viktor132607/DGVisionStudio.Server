using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Slideshow;

public sealed class HomeSlideshowSettingsServiceTests
{
    [Fact]
    public async Task UpdateAndRead_NormalizesIdsAndClampsInterval()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new HomeSlideshowSettingsService(context);

        await service.UpdateAsync(
            new UpdateHomeSlideshowRequest
            {
                ImageIds = [3, 3, -1, 2, 99],
                UseDefaultInterval = false,
                IntervalMs = 500
            },
            availableIds: [2, 3]);

        (await service.GetSelectedImageIdsAsync()).Should().Equal(3, 2);
        var response = await service.GetResponseAsync();
        response.UseDefaultInterval.Should().BeFalse();
        response.IntervalMs.Should().Be(1000);
        (await context.SiteSettings.CountAsync()).Should().Be(1);
    }
}

public sealed class HomeSlideshowImageServiceTests
{
    [Fact]
    public async Task GetSlideshowImagesAsync_ReturnsOnlyAvailablePublishedImagesInDefaultOrder()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory
        {
            Key = "portfolio",
            Name = "Portfolio",
            NameEn = "Portfolio",
            DisplayOrder = 1,
            IsActive = true
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "album",
            Title = "Album",
            DisplayOrder = 1,
            IsPublished = true,
            IsUserUploaded = false
        };
        var visible = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/uploads/visible.jpg",
            DisplayOrder = 1,
            IsPublished = true
        };
        var hidden = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/uploads/hidden.jpg",
            DisplayOrder = 2,
            IsPublished = false
        };
        context.AddRange(category, album, visible, hidden);
        await context.SaveChangesAsync();
        var environment = CreateEnvironment();
        var settings = new HomeSlideshowSettingsService(context);
        var video = new HomeSlideshowVideoService(context, environment);
        var service = new HomeSlideshowImageService(context, settings, video);

        var result = await service.GetSlideshowImagesAsync();

        result.Should().ContainSingle(x =>
            x.Id == visible.Id &&
            x.ImageUrl == visible.ImageUrl &&
            x.AlbumTitle == "Album");
    }

    private static TestWebHostEnvironment CreateEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dg-slideshow-images-{Guid.NewGuid():N}");
        return new TestWebHostEnvironment
        {
            ContentRootPath = root,
            WebRootPath = Path.Combine(root, "wwwroot")
        };
    }
}

public sealed class HomeSlideshowVideoServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"dg-slideshow-video-{Guid.NewGuid():N}");

    [Fact]
    public async Task UploadGetAndDelete_PersistsAndClearsVideoSetting()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = _root,
            WebRootPath = Path.Combine(_root, "wwwroot")
        };
        var service = new HomeSlideshowVideoService(context, environment);
        var file = GalleryTestFiles.Create("intro.mp4", "video/mp4", [1, 2, 3, 4]);

        var uploaded = await service.UploadAsync(file);
        var loaded = await service.GetAsync();

        uploaded.VideoUrl.Should().StartWith("/uploads/portfolio/home-intro-")
            .And.EndWith(".mp4");
        loaded.VideoUrl.Should().Be(uploaded.VideoUrl);
        File.Exists(Path.Combine(
            environment.WebRootPath,
            uploaded.VideoUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeTrue();

        await service.DeleteAsync();

        (await service.GetAsync()).VideoUrl.Should().BeNull();
        (await context.SiteSettings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_RejectsUnsupportedFile()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = _root,
            WebRootPath = Path.Combine(_root, "wwwroot")
        };
        var service = new HomeSlideshowVideoService(context, environment);
        var file = GalleryTestFiles.Create("payload.exe", "application/octet-stream", [1]);

        var action = () => service.UploadAsync(file);

        await action.Should().ThrowAsync<SlideshowValidationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

public sealed class HomeSlideshowServiceTests
{
    [Fact]
    public async Task Facade_DelegatesImageSettingsAndVideoQueries()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var root = Path.Combine(Path.GetTempPath(), $"dg-slideshow-facade-{Guid.NewGuid():N}");
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = root,
            WebRootPath = Path.Combine(root, "wwwroot")
        };
        var settings = new HomeSlideshowSettingsService(context);
        var video = new HomeSlideshowVideoService(context, environment);
        var images = new HomeSlideshowImageService(context, settings, video);
        var service = new HomeSlideshowService(images, settings, video);

        var slideshow = await service.GetSlideshowImagesAsync();
        var settingsResponse = await service.GetSettingsAsync();
        var intro = await service.GetIntroVideoAsync();

        slideshow.Should().BeEmpty();
        settingsResponse.UseDefaultInterval.Should().BeTrue();
        settingsResponse.IntervalMs.Should().Be(settingsResponse.DefaultIntervalMs);
        intro.VideoUrl.Should().BeNull();
    }
}
