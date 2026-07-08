using System.Text.Json;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class SlideshowControllerTests
{
    [Fact]
    public async Task PortfolioSlideshow_GetSlideshowImages_ReturnsAvailableImages_WhenNoSavedSelectionExists()
    {
        await using var context = CreateContext();
        var album = await SeedVisibleAlbum(context);
        context.PortfolioImages.AddRange(
            new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/second.jpg", IsPublished = true, DisplayOrder = 2 },
            new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/first.jpg", IsPublished = true, DisplayOrder = 1 },
            new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/hidden.jpg", IsPublished = false, DisplayOrder = 3 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioSlideshowController(context);

        var result = await controller.GetSlideshowImages();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var images = okResult.Value.Should().BeAssignableTo<IEnumerable<SlideshowImageDto>>().Which.ToList();
        images.Select(x => x.ImageUrl).Should().Equal("/images/first.jpg", "/images/second.jpg");
        images.Should().OnlyContain(x => x.IsPublished);
    }

    [Fact]
    public async Task PortfolioSlideshow_GetSlideshowImages_ReturnsSavedSelection_InSavedOrder()
    {
        await using var context = CreateContext();
        var album = await SeedVisibleAlbum(context);
        var first = new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/first.jpg", IsPublished = true, DisplayOrder = 1 };
        var second = new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/second.jpg", IsPublished = true, DisplayOrder = 2 };
        context.PortfolioImages.AddRange(first, second);
        await context.SaveChangesAsync();
        context.SiteSettings.Add(new SiteSetting
        {
            Key = "home-slideshow-image-ids",
            Value = JsonSerializer.Serialize(new { imageIds = new[] { second.Id, first.Id } })
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioSlideshowController(context);

        var result = await controller.GetSlideshowImages();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var images = okResult.Value.Should().BeAssignableTo<IEnumerable<SlideshowImageDto>>().Which.ToList();
        images.Select(x => x.ImageUrl).Should().Equal("/images/second.jpg", "/images/first.jpg");
        images.Select(x => x.SlideshowOrder).Should().Equal(1, 2);
        images.Should().OnlyContain(x => x.IsSelected);
    }

    [Fact]
    public async Task PortfolioSlideshow_GetIntroVideo_ReturnsSavedIntroVideoUrl()
    {
        await using var context = CreateContext();
        context.SiteSettings.Add(new SiteSetting
        {
            Key = "home-slideshow-intro-video-url",
            Value = " /uploads/slideshow/intro.mp4 "
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioSlideshowController(context);

        var result = await controller.GetIntroVideo();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var response = okResult.Value.Should().BeOfType<SlideshowIntroVideoResponse>().Which;
        response.VideoUrl.Should().Be("/uploads/slideshow/intro.mp4");
    }

    [Fact]
    public async Task PortfolioSlideshow_GetSettings_ClampsCustomInterval()
    {
        await using var context = CreateContext();
        context.SiteSettings.Add(new SiteSetting
        {
            Key = "home-slideshow-image-ids",
            Value = JsonSerializer.Serialize(new { imageIds = Array.Empty<int>(), intervalMs = 999999 })
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioSlideshowController(context);

        var result = await controller.GetSettings();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var response = okResult.Value.Should().BeOfType<SlideshowSettingsResponse>().Which;
        response.UseDefaultInterval.Should().BeFalse();
        response.IntervalMs.Should().Be(30000);
    }

    [Fact]
    public async Task AdminSlideshow_UpdateSlideshow_SavesOnlyAvailableDistinctIds_AndClampsInterval()
    {
        await using var context = CreateContext();
        var album = await SeedVisibleAlbum(context);
        var first = new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/first.jpg", IsPublished = true, DisplayOrder = 1 };
        var hidden = new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/hidden.jpg", IsPublished = false, DisplayOrder = 2 };
        context.PortfolioImages.AddRange(first, hidden);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateAdminController(context);

        var result = await controller.UpdateSlideshow(new UpdateHomeSlideshowRequest
        {
            ImageIds = [first.Id, hidden.Id, first.Id, -5, 404],
            UseDefaultInterval = false,
            IntervalMs = 500
        });

        result.Should().BeOfType<NoContentResult>();
        var setting = await context.SiteSettings.SingleAsync(x => x.Key == "home-slideshow-image-ids");
        setting.Value.Should().Contain(first.Id.ToString());
        setting.Value.Should().NotContain(hidden.Id.ToString());
        setting.Value.Should().Contain("1000");
    }

    [Fact]
    public async Task AdminSlideshow_UploadIntroVideo_ReturnsBadRequest_WhenFileIsEmpty()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.UploadIntroVideo(new FormFile(Stream.Null, 0, 0, "file", "empty.mp4")
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/mp4"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static async Task<PortfolioAlbum> SeedVisibleAlbum(AppDbContext context)
    {
        var category = new PortfolioCategory { Key = "weddings", Name = "Weddings", NameEn = "Weddings", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "weddings",
            Title = "Weddings",
            IsPublished = true,
            IsUserUploaded = false
        };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        return album;
    }

    private static AdminSlideshowController CreateAdminController(AppDbContext context)
    {
        return new AdminSlideshowController(context, new TestWebHostEnvironment());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }
}
