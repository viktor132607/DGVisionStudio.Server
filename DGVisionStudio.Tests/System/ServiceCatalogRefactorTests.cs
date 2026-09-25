using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CatalogService = DGVisionStudio.Domain.Entities.Service;

namespace DGVisionStudio.Tests.System;

public sealed class ServiceCatalogRefactorTests
{
    [Fact]
    public void InputService_PreservesValidationAndNormalization()
    {
        var input = new ServiceCatalogInputService();

        input.Validate(new ServiceCardDto
        {
            Title = " ",
            Description = "Description"
        }).Should().Be("Заглавието е задължително.");

        input.Validate(new ServiceCardDto
        {
            Title = "Title",
            Description = " "
        }).Should().Be("Описанието е задължително.");

        var created = input.Create(
            new ServiceCardDto
            {
                Title = "  Title  ",
                ShortDescription = "  Short  ",
                Description = "  Description  ",
                CoverImageUrl = "  /cover.jpg  ",
                IsActive = true
            },
            displayOrder: 5,
            createdAtUtc: new DateTime(
                2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));

        created.Title.Should().Be("Title");
        created.ShortDescription.Should().Be("Short");
        created.Description.Should().Be("Description");
        created.CoverImageUrl.Should().Be("/cover.jpg");
        created.DisplayOrder.Should().Be(5);
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task OrderingService_PreservesDuplicateUnknownAndAppendSemantics()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Item("First", 1);
        var second = Item("Second", 2);
        var third = Item("Third", 3);
        fixture.Context.Services.AddRange(first, second, third);
        await fixture.Context.SaveChangesAsync();

        var ordering =
            new ServiceCatalogOrderingService(fixture.Context);

        var result = await ordering.ReorderAsync(
            new[] { third.Id, third.Id, 999999, first.Id });

        result.Select(item => item.Id)
            .Should().Equal(third.Id, first.Id, second.Id);
        result.Select(item => item.DisplayOrder)
            .Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task CommandService_CreatePreservesNextOrderAndCreatedResponse()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        fixture.Context.Services.Add(Item("Existing", 7));
        await fixture.Context.SaveChangesAsync();

        var ordering =
            new ServiceCatalogOrderingService(fixture.Context);
        var service = new ServiceCatalogCommandService(
            fixture.Context,
            new ServiceCatalogInputService(),
            ordering);

        var result = await service.CreateAsync(new ServiceCardDto
        {
            Title = " New ",
            Description = " Description ",
            IsActive = true
        });

        result.StatusCode.Should()
            .Be(StatusCodes.Status201Created);
        var item = result.Value.Should()
            .BeOfType<CatalogService>().Subject;
        item.DisplayOrder.Should().Be(8);
        item.Title.Should().Be("New");
        item.Description.Should().Be("Description");
    }

    [Fact]
    public async Task FacadeCompatibilityConstructor_PreservesQueriesAndCommands()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        fixture.Context.Services.Add(Item("Existing", 1));
        await fixture.Context.SaveChangesAsync();

        var service = new ServiceCatalogService(fixture.Context);

        var all = await service.GetAllAsync();
        var deleted = await service.DeleteAsync(
            fixture.Context.Services.Single().Id);

        all.Value.Should()
            .BeOfType<List<CatalogService>>()
            .Which.Should().ContainSingle();
        deleted.StatusCode.Should()
            .Be(StatusCodes.Status204NoContent);
        (await fixture.Context.Services.CountAsync())
            .Should().Be(0);
    }

    private static CatalogService Item(string title, int order) =>
        new()
        {
            Title = title,
            Description = $"{title} description",
            DisplayOrder = order,
            IsActive = true
        };
}
