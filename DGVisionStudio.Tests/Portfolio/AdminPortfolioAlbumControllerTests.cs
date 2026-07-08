using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class AdminPortfolioAlbumControllerTests
{
    [Fact]
    public async Task CreateAlbum_ReturnsBadRequest_WhenCategoryDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateAlbum),
            "PortfolioCategoryId", 404,
            "Slug", "weddings",
            "Title", "Weddings",
            "IsPublished", true,
            "DisplayOrder", 1);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateAlbum), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioAlbums.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAlbum_ReturnsBadRequest_WhenSlugIsMissing()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "weddings", Name = "Weddings", NameEn = "Weddings", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateAlbum),
            "PortfolioCategoryId", category.Id,
            "Slug", " ",
            "Title", "Weddings",
            "IsPublished", true,
            "DisplayOrder", 1);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateAlbum), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioAlbums.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAlbum_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "weddings", Name = "Weddings", NameEn = "Weddings", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateAlbum),
            "PortfolioCategoryId", category.Id,
            "Slug", "weddings",
            "Title", " ",
            "IsPublished", true,
            "DisplayOrder", 1);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateAlbum), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioAlbums.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAlbum_ReturnsBadRequest_WhenSlugAlreadyExists()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "weddings", Name = "Weddings", NameEn = "Weddings", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        context.PortfolioAlbums.Add(new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "weddings",
            Title = "Existing Weddings",
            IsPublished = true,
            IsUserUploaded = false
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.CreateAlbum),
            "PortfolioCategoryId", category.Id,
            "Slug", "weddings",
            "Title", "New Weddings",
            "IsPublished", true,
            "DisplayOrder", 1);

        var result = await Invoke(controller, nameof(AdminPortfolioController.CreateAlbum), request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioAlbums.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateAlbum_ReturnsNotFound_WhenAlbumDoesNotExist()
    {
        await using var context = CreateContext();
        var category = new PortfolioCategory { Key = "weddings", Name = "Weddings", NameEn = "Weddings", IsActive = true };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(AdminPortfolioController.UpdateAlbum),
            "PortfolioCategoryId", category.Id,
            "Slug", "weddings",
            "Title", "Weddings",
            "IsPublished", true,
            "DisplayOrder", 1);

        var result = await Invoke(controller, nameof(AdminPortfolioController.UpdateAlbum), 404, request);

        result.Should().BeOfType<NotFoundResult>();
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
