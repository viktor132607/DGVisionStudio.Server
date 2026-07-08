using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class AdminPortfolioControllerTests
{
    [Fact]
    public async Task GetCategories_ReturnsCategories_OrderedByDisplayOrderThenId()
    {
        await using var context = CreateContext();
        context.PortfolioCategories.AddRange(
            new PortfolioCategory { Key = "third", Name = "Third", NameEn = "Third", DisplayOrder = 3 },
            new PortfolioCategory { Key = "first", Name = "First", NameEn = "First", DisplayOrder = 1 },
            new PortfolioCategory { Key = "second", Name = "Second", NameEn = "Second", DisplayOrder = 2 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);

        var result = await controller.GetCategories();

        var okResult = result.Should().BeOfType<OkObjectResult>().Which;
        var categories = okResult.Value.Should().BeAssignableTo<IEnumerable<PortfolioCategory>>().Which.ToList();
        categories.Select(x => x.Key).Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task GetCategoryById_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.GetCategoryById(404);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateCategory_ReturnsBadRequest_WhenNameIsMissing()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(InvokeCreateCategory),
            "Key", "weddings",
            "Name", " ",
            "NameEn", "Weddings",
            "DisplayOrder", 1,
            "IsActive", true);

        var result = await InvokeCreateCategory(controller, request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCategory_ReturnsBadRequest_WhenKeyAlreadyExists()
    {
        await using var context = CreateContext();
        context.PortfolioCategories.Add(new PortfolioCategory
        {
            Key = "weddings",
            Name = "Weddings",
            NameEn = "Weddings",
            DisplayOrder = 1,
            IsActive = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = CreateController(context);
        var request = CreateRequest(
            nameof(InvokeCreateCategory),
            "Key", " WEDDINGS ",
            "Name", "New Weddings",
            "NameEn", "New Weddings",
            "DisplayOrder", 2,
            "IsActive", true);

        var result = await InvokeCreateCategory(controller, request);

        result.Should().BeOfType<BadRequestObjectResult>();
        context.PortfolioCategories.Should().ContainSingle();
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

    private static object CreateRequest(string invokerName, params object?[] propertyPairs)
    {
        var methodName = invokerName.Replace("Invoke", string.Empty, StringComparison.Ordinal);
        var requestType = typeof(AdminPortfolioController)
            .GetMethod(methodName)!
            .GetParameters()[0]
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

    private static async Task<IActionResult> InvokeCreateCategory(AdminPortfolioController controller, object request)
    {
        var resultTask = (Task<IActionResult>)typeof(AdminPortfolioController)
            .GetMethod(nameof(AdminPortfolioController.CreateCategory))!
            .Invoke(controller, new[] { request })!;

        return await resultTask;
    }
}
