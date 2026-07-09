using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Pricing;

public sealed class PricingServiceTests
{
    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveItems_OrderedByDisplayOrderThenId()
    {
        await using var context = CreateContext();
        context.PricingItems.AddRange(
            new PricingItem { Title = "Second", Description = "Description", PricingMode = "Fixed", PriceText = "20 EUR", DisplayOrder = 2, IsActive = true },
            new PricingItem { Title = "Hidden", Description = "Description", PricingMode = "Fixed", PriceText = "30 EUR", DisplayOrder = 3, IsActive = false },
            new PricingItem { Title = "First", Description = "Description", PricingMode = "Fixed", PriceText = "10 EUR", DisplayOrder = 1, IsActive = true });
        await context.SaveChangesAsync();

        var service = new PricingService(context);

        var result = await service.GetActiveAsync();

        result.Select(x => x.Title).Should().Equal("First", "Second");
        result.Should().OnlyContain(x => x.IsActive);
    }

    [Fact]
    public async Task CreateAsync_AddsItem_WithNextDisplayOrderAndNormalizedValues()
    {
        await using var context = CreateContext();
        context.PricingItems.Add(new PricingItem { Title = "Existing", Description = "Description", PricingMode = "Fixed", PriceText = "10 EUR", DisplayOrder = 4, IsActive = true });
        await context.SaveChangesAsync();

        var service = new PricingService(context);

        var result = await service.CreateAsync(new PricingItemRequest
        {
            Title = " Portrait ",
            Description = " Description ",
            PricingMode = "fixed",
            PriceText = " 60 EUR ",
            IsActive = true
        });

        result.Title.Should().Be("Portrait");
        result.Description.Should().Be("Description");
        result.PricingMode.Should().Be("Fixed");
        result.PriceText.Should().Be("60 EUR");
        result.DisplayOrder.Should().Be(5);

        var saved = await context.PricingItems.SingleAsync(x => x.Id == result.Id);
        saved.Title.Should().Be("Portrait");
    }

    [Fact]
    public async Task CreateAsync_ClearsPriceText_WhenPricingModeIsNegotiable()
    {
        await using var context = CreateContext();
        var service = new PricingService(context);

        var result = await service.CreateAsync(new PricingItemRequest
        {
            Title = "Product",
            Description = "Description",
            PricingMode = "Negotiable",
            PriceText = "100 EUR"
        });

        result.PricingMode.Should().Be("Negotiable");
        result.PriceText.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenItemDoesNotExist()
    {
        await using var context = CreateContext();
        var service = new PricingService(context);

        var result = await service.UpdateAsync(404, new PricingItemRequest
        {
            Title = "Portrait",
            Description = "Description",
            PricingMode = "Fixed",
            PriceText = "60 EUR"
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReorderAsync_ReordersSpecifiedItems_AndAppendsRemaining()
    {
        await using var context = CreateContext();
        var first = new PricingItem { Title = "First", Description = "Description", PricingMode = "Fixed", PriceText = "10 EUR", DisplayOrder = 1 };
        var second = new PricingItem { Title = "Second", Description = "Description", PricingMode = "Fixed", PriceText = "20 EUR", DisplayOrder = 2 };
        var third = new PricingItem { Title = "Third", Description = "Description", PricingMode = "Fixed", PriceText = "30 EUR", DisplayOrder = 3 };
        context.PricingItems.AddRange(first, second, third);
        await context.SaveChangesAsync();

        var service = new PricingService(context);

        var result = await service.ReorderAsync(new ReorderPricingItemsRequest { Ids = [third.Id, first.Id] });

        result.Select(x => x.Id).Should().Equal(third.Id, first.Id, second.Id);
        (await context.PricingItems.SingleAsync(x => x.Id == third.Id)).DisplayOrder.Should().Be(1);
        (await context.PricingItems.SingleAsync(x => x.Id == first.Id)).DisplayOrder.Should().Be(2);
        (await context.PricingItems.SingleAsync(x => x.Id == second.Id)).DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem_AndNormalizesDisplayOrder()
    {
        await using var context = CreateContext();
        var first = new PricingItem { Title = "First", Description = "Description", PricingMode = "Fixed", PriceText = "10 EUR", DisplayOrder = 1 };
        var second = new PricingItem { Title = "Second", Description = "Description", PricingMode = "Fixed", PriceText = "20 EUR", DisplayOrder = 2 };
        var third = new PricingItem { Title = "Third", Description = "Description", PricingMode = "Fixed", PriceText = "30 EUR", DisplayOrder = 3 };
        context.PricingItems.AddRange(first, second, third);
        await context.SaveChangesAsync();

        var service = new PricingService(context);

        var deleted = await service.DeleteAsync(second.Id);

        deleted.Should().BeTrue();
        var remaining = await context.PricingItems.OrderBy(x => x.DisplayOrder).ToListAsync();
        remaining.Select(x => x.Id).Should().Equal(first.Id, third.Id);
        remaining.Select(x => x.DisplayOrder).Should().Equal(1, 2);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
