using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioControllerTests
{
    [Fact]
    public async Task GetCategories_ReturnsOnlyActiveCategories_OrderedByDisplayOrderThenId()
    {
        await using var context = CreateContext();
        context.PortfolioCategories.AddRange(
            new PortfolioCategory { Key = "inactive", Name = "Inactive", NameEn = "Inactive", DisplayOrder = 1, IsActive = false },
            new PortfolioCategory { Key = "weddings", Name = "Weddings", NameEn = "Weddings", DisplayOrder = 2, IsActive = true },
            new PortfolioCategory { Key = "events", Name = "Events", NameEn = "Events", DisplayOrder = 1, IsActive = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetCategories();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var categories = okResult.Value.Should().BeAssignableTo<IEnumerable<PortfolioCategory>>().Which.ToList();
        categories.Should().HaveCount(2);
        categories.Select(x => x.Key).Should().Equal("events", "weddings");
        categories.Should().OnlyContain(x => x.IsActive);
    }

    [Fact]
    public async Task GetAlbums_ReturnsPublishedNonUserUploadedAlbums_WithActiveCategoryOnly()
    {
        await using var context = CreateContext();
        var activeCategory = new PortfolioCategory { Key = "active", Name = "Active", NameEn = "Active", IsActive = true };
        var inactiveCategory = new PortfolioCategory { Key = "inactive", Name = "Inactive", NameEn = "Inactive", IsActive = false };
        context.PortfolioCategories.AddRange(activeCategory, inactiveCategory);
        await context.SaveChangesAsync();

        context.PortfolioAlbums.AddRange(
            new PortfolioAlbum { PortfolioCategoryId = activeCategory.Id, Slug = "visible", Title = "Visible", IsPublished = true, IsUserUploaded = false, DisplayOrder = 2, CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new PortfolioAlbum { PortfolioCategoryId = activeCategory.Id, Slug = "hidden", Title = "Hidden", IsPublished = false, IsUserUploaded = false, DisplayOrder = 1 },
            new PortfolioAlbum { PortfolioCategoryId = activeCategory.Id, Slug = "user-uploaded", Title = "User Uploaded", IsPublished = true, IsUserUploaded = true, DisplayOrder = 1 },
            new PortfolioAlbum { PortfolioCategoryId = inactiveCategory.Id, Slug = "inactive-category", Title = "Inactive Category", IsPublished = true, IsUserUploaded = false, DisplayOrder = 1 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetAlbums();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var albums = okResult.Value.Should().BeAssignableTo<IEnumerable<PortfolioAlbum>>().Which.ToList();
        albums.Should().ContainSingle();
        albums[0].Slug.Should().Be("visible");
    }

    [Fact]
    public async Task GetAlbums_WhenCategoryIdProvided_FiltersByCategory()
    {
        await using var context = CreateContext();
        var firstCategory = new PortfolioCategory { Key = "first", Name = "First", NameEn = "First", IsActive = true };
        var secondCategory = new PortfolioCategory { Key = "second", Name = "Second", NameEn = "Second", IsActive = true };
        context.PortfolioCategories.AddRange(firstCategory, secondCategory);
        await context.SaveChangesAsync();

        context.PortfolioAlbums.AddRange(
            new PortfolioAlbum { PortfolioCategoryId = firstCategory.Id, Slug = "first-album", Title = "First Album", IsPublished = true, IsUserUploaded = false },
            new PortfolioAlbum { PortfolioCategoryId = secondCategory.Id, Slug = "second-album", Title = "Second Album", IsPublished = true, IsUserUploaded = false });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetAlbums(firstCategory.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var albums = okResult.Value.Should().BeAssignableTo<IEnumerable<PortfolioAlbum>>().Which.ToList();
        albums.Should().ContainSingle();
        albums[0].Slug.Should().Be("first-album");
    }

    [Fact]
    public async Task GetAlbum_ReturnsNotFound_WhenAlbumIsNotPublished()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "category", Name = "Category", NameEn = "Category", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        context.PortfolioAlbums.Add(new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "draft-album",
            Title = "Draft Album",
            IsPublished = false,
            IsUserUploaded = false
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetAlbum("draft-album");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAlbum_ReturnsOnlyPublishedImages_WhenAlbumIsVisible()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "category", Name = "Category", NameEn = "Category", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "visible-album",
            Title = "Visible Album",
            IsPublished = true,
            IsUserUploaded = false
        };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();

        context.PortfolioImages.AddRange(
            new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/visible.jpg", IsPublished = true, DisplayOrder = 2 },
            new PortfolioImage { PortfolioAlbumId = album.Id, ImageUrl = "/images/hidden.jpg", IsPublished = false, DisplayOrder = 1 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetAlbum("visible-album");

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var returnedAlbum = okResult.Value.Should().BeOfType<PortfolioAlbum>().Which;
        returnedAlbum.Images.Should().ContainSingle();
        returnedAlbum.Images.Single().ImageUrl.Should().Be("/images/visible.jpg");
    }

    [Fact]
    public async Task GetImages_ReturnsOnlyPublishedImages_FromVisibleAlbumsAndActiveCategories()
    {
        await using var context = CreateContext();
        var activeCategory = new PortfolioCategory { Key = "active", Name = "Active", NameEn = "Active", IsActive = true };
        var inactiveCategory = new PortfolioCategory { Key = "inactive", Name = "Inactive", NameEn = "Inactive", IsActive = false };
        context.PortfolioCategories.AddRange(activeCategory, inactiveCategory);
        await context.SaveChangesAsync();

        var visibleAlbum = new PortfolioAlbum { PortfolioCategoryId = activeCategory.Id, Slug = "visible", Title = "Visible", IsPublished = true, IsUserUploaded = false };
        var draftAlbum = new PortfolioAlbum { PortfolioCategoryId = activeCategory.Id, Slug = "draft", Title = "Draft", IsPublished = false, IsUserUploaded = false };
        var userUploadedAlbum = new PortfolioAlbum { PortfolioCategoryId = activeCategory.Id, Slug = "user-uploaded", Title = "User Uploaded", IsPublished = true, IsUserUploaded = true };
        var inactiveCategoryAlbum = new PortfolioAlbum { PortfolioCategoryId = inactiveCategory.Id, Slug = "inactive-category", Title = "Inactive Category", IsPublished = true, IsUserUploaded = false };
        context.PortfolioAlbums.AddRange(visibleAlbum, draftAlbum, userUploadedAlbum, inactiveCategoryAlbum);
        await context.SaveChangesAsync();

        context.PortfolioImages.AddRange(
            new PortfolioImage { PortfolioAlbumId = visibleAlbum.Id, ImageUrl = "/images/visible.mp4", IsPublished = true, DisplayOrder = 2 },
            new PortfolioImage { PortfolioAlbumId = visibleAlbum.Id, ImageUrl = "/images/hidden.jpg", IsPublished = false, DisplayOrder = 1 },
            new PortfolioImage { PortfolioAlbumId = draftAlbum.Id, ImageUrl = "/images/draft.jpg", IsPublished = true },
            new PortfolioImage { PortfolioAlbumId = userUploadedAlbum.Id, ImageUrl = "/images/user.jpg", IsPublished = true },
            new PortfolioImage { PortfolioAlbumId = inactiveCategoryAlbum.Id, ImageUrl = "/images/inactive.jpg", IsPublished = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetImages();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var images = okResult.Value.Should().BeAssignableTo<IEnumerable<object>>().Which.ToList();
        images.Should().ContainSingle();
        GetPropertyValue<string>(images[0], "ImageUrl").Should().Be("/images/visible.mp4");
        GetPropertyValue<string>(images[0], "mediaType").Should().Be("Video");
        GetPropertyValue<string>(images[0], "contentType").Should().Be("video/mp4");
    }

    [Fact]
    public async Task GetImages_WhenAlbumIdProvided_FiltersByAlbum()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "category", Name = "Category", NameEn = "Category", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        var firstAlbum = new PortfolioAlbum { PortfolioCategoryId = category.Id, Slug = "first", Title = "First", IsPublished = true, IsUserUploaded = false };
        var secondAlbum = new PortfolioAlbum { PortfolioCategoryId = category.Id, Slug = "second", Title = "Second", IsPublished = true, IsUserUploaded = false };
        context.PortfolioAlbums.AddRange(firstAlbum, secondAlbum);
        await context.SaveChangesAsync();

        context.PortfolioImages.AddRange(
            new PortfolioImage { PortfolioAlbumId = firstAlbum.Id, ImageUrl = "/images/first.jpg", IsPublished = true },
            new PortfolioImage { PortfolioAlbumId = secondAlbum.Id, ImageUrl = "/images/second.jpg", IsPublished = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new PortfolioController(context);

        var result = await controller.GetImages(firstAlbum.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var images = okResult.Value.Should().BeAssignableTo<IEnumerable<object>>().Which.ToList();
        images.Should().ContainSingle();
        GetPropertyValue<string>(images[0], "ImageUrl").Should().Be("/images/first.jpg");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static T? GetPropertyValue<T>(object source, string propertyName)
    {
        return (T?)source.GetType().GetProperty(propertyName)?.GetValue(source);
    }
}
