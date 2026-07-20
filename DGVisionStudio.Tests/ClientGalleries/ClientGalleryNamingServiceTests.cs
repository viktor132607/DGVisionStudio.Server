using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryNamingServiceTests
{
    [Fact]
    public void Slugify_NormalizesWhitespaceSymbolsAndBulgarianText()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = new ClientGalleryNamingService(context);

        var slug = service.Slugify("  Сватба   Иван & Мария  ");

        slug.Should().Be("сватба-иван-мария");
    }

    [Fact]
    public async Task BuildUniqueSlugAsync_AddsTheNextAvailableSuffix()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.PortfolioAlbums.AddRange(
            new PortfolioAlbum { Title = "Gallery", Slug = "gallery" },
            new PortfolioAlbum { Title = "Gallery 2", Slug = "gallery-2" });
        await context.SaveChangesAsync();
        var service = new ClientGalleryNamingService(context);

        var slug = await service.BuildUniqueSlugAsync("Gallery");

        slug.Should().Be("gallery-3");
    }

    [Fact]
    public async Task BuildUniqueSlugAsync_IgnoresTheCurrentAlbum()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum { Title = "Gallery", Slug = "gallery" };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new ClientGalleryNamingService(context);

        var slug = await service.BuildUniqueSlugAsync("Gallery", album.Id);

        slug.Should().Be("gallery");
    }

    [Fact]
    public async Task EnsureClientAlbumsCategoryAsync_RestoresAndNormalizesExistingCategory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var category = new PortfolioCategory
        {
            Key = "client-galleries",
            Name = string.Empty,
            NameEn = string.Empty,
            IsActive = true,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();
        var service = new ClientGalleryNamingService(context);

        var id = await service.EnsureClientAlbumsCategoryAsync();

        id.Should().Be(category.Id);
        var stored = await context.PortfolioCategories.IgnoreQueryFilters().SingleAsync();
        stored.IsDeleted.Should().BeFalse();
        stored.DeletedAtUtc.Should().BeNull();
        stored.IsActive.Should().BeFalse();
        stored.Name.Should().Be("Клиентски албуми");
        stored.NameEn.Should().Be("Client Galleries");
    }
}
