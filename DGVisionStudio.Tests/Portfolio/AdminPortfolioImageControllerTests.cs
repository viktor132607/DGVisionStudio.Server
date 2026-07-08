using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class AdminPortfolioImageControllerTests
{
    [Fact]
    public async Task CreateImage_ReturnsBadRequest_WhenAlbumDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateImage),
            "PortfolioAlbumId", 404,
            "ImageUrl", "/images/photo.jpg",
            "DisplayOrder", 1,
            "IsCover", false,
            "IsPublished", true);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateImage), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioImages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateImage_ReturnsBadRequest_WhenAlbumIsUserUploaded()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "clients", Name = "Clients", NameEn = "Clients", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "client-upload",
            Title = "Client Upload",
            IsPublished = true,
            IsUserUploaded = true
        };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateImage),
            "PortfolioAlbumId", album.Id,
            "ImageUrl", "/images/photo.jpg",
            "DisplayOrder", 1,
            "IsCover", false,
            "IsPublished", true);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateImage), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioImages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateImage_ReturnsBadRequest_WhenImageUrlIsMissing()
    {
        await using var context = CreateContext();
        var album = await SeedVisibleAlbum(context);
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateImage),
            "PortfolioAlbumId", album.Id,
            "ImageUrl", " ",
            "DisplayOrder", 1,
            "IsCover", false,
            "IsPublished", true);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateImage), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioImages.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateImage_ReturnsNotFound_WhenImageDoesNotExist()
    {
        await using var context = CreateContext();
        var album = await SeedVisibleAlbum(context);
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.UpdateImage),
            "PortfolioAlbumId", album.Id,
            "ImageUrl", "/images/photo.jpg",
            "DisplayOrder", 1,
            "IsCover", false,
            "IsPublished", true);

        var result = await Invoke(controller, nameof(AdminPortfolioController.UpdateImage), 404, request);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteImage_ReturnsNotFound_WhenImageDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.DeleteImage(404);

        result.Should().BeOfType<NotFoundResult>();
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

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static AdminPortfolioController CreateController(AppDbContext context)
    {
        return (AdminPortfolioController)Activator.CreateInstance(
            typeof(AdminPortfolioController),
            context,
            null!,
            null!)!;
    }

    private static object CreateRequest(string actionName, params object?[] propertyPairs)
    {
        var requestType = typeof(AdminPortfolioController)
            .GetMethod(actionName)!
            .GetParameters()
            .Last()
            .ParameterType;

        var request = Activator.CreateInstance(requestType)!;

        for (var i = 0; i < propertyPairs.Length; i += 2)
        {
            var propertyName = (string)propertyPairs[i]!;
            var value = propertyPairs[i + 1];
            requestType.GetProperty(propertyName)!.SetValue(request, value);
        }

        return request;
    }

    private static async Task<IActionResult> Invoke(AdminPortfolioController controller, string actionName, params object[] parameters)
    {
        var resultTask = (Task<IActionResult>)typeof(AdminPortfolioController)
            .GetMethod(actionName)!
            .Invoke(controller, parameters)!;

        return await resultTask;
    }
}
