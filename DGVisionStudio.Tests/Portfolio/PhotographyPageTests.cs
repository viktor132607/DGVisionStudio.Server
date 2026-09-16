using System.Text.Json;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PhotographyPageTests
{
    [Fact]
    public void MigrationOnlyCreatesServicePageTableAndIndexes()
    {
        var migration = new DGVisionStudio.Infrastructure.Migrations.AddPhotographyPages();
        var table = Assert.Single(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>());
        Assert.Equal("PhotographyPages", table.Name);
        Assert.Equal(3, migration.UpOperations.Count);
        Assert.All(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>(), index => Assert.Equal("PhotographyPages", index.Table));
        var drop = Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal("PhotographyPages", drop.Name);
    }

    [Fact]
    public async Task SeedPreservesEditsAndDeletedPagesAcrossRestarts()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var db = fixture.Context;
        db.PortfolioCategories.Add(new PortfolioCategory { Key = "wedding", Name = "Сватби" });
        await db.SaveChangesAsync();
        await PhotographyPageSeeder.SeedAsync(db);
        Assert.Equal(10, await db.PhotographyPages.CountAsync());
        var page = await db.PhotographyPages.SingleAsync(x => x.Slug == "svatben-fotograf-ruse");
        Assert.NotNull(page.PortfolioCategoryId);
        page.Title = "Редактирано";
        await db.SaveChangesAsync();
        await PhotographyPageSeeder.SeedAsync(db);
        Assert.Equal("Редактирано", page.Title);
        var service = new PhotographyPageService(db);
        Assert.Equal(204, (await service.DeleteAsync(page.Id)).StatusCode);
        await PhotographyPageSeeder.SeedAsync(db);
        Assert.Equal(9, await db.PhotographyPages.CountAsync());
        Assert.Equal(10, await db.PhotographyPages.IgnoreQueryFilters().CountAsync());
        Assert.Equal(200, (await service.SaveAsync(null, new PhotographyPageInput { Slug = page.Slug, Title = "Нова услуга" })).StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateEditDeactivateDeleteAndPublicListHidesInactive()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new PhotographyPageService(fixture.Context);
        var input = new PhotographyPageInput { Slug = "custom-service", Title = "Моята услуга", Body = "Описание", TitleEn = "My service" };
        var result = await service.SaveAsync(null, input);
        Assert.Equal(200, result.StatusCode);
        var page = Assert.IsType<PhotographyPage>(result.Value);
        Assert.Single(await service.ListAsync());
        input.IsActive = false; input.Title = "Променена";
        Assert.Equal(200, (await service.SaveAsync(page.Id, input)).StatusCode);
        Assert.Empty(await service.ListAsync());
        Assert.Equal("Променена", Assert.Single(await service.ListAsync(true)).Title);
        Assert.Equal(404, (await service.AlbumsAsync(page.Slug)).StatusCode);
        Assert.Equal(204, (await service.DeleteAsync(page.Id)).StatusCode);
        Assert.Equal(404, (await service.SaveAsync(page.Id, input)).StatusCode);
        Assert.Empty(await service.ListAsync(true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../../admin")]
    [InlineData("bad?path")]
    [InlineData("two words")]
    public async Task RejectsInvalidAddresses(string slug)
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new PhotographyPageService(fixture.Context);
        Assert.Equal(400, (await service.SaveAsync(null, new PhotographyPageInput { Slug = slug, Title = "Test" })).StatusCode);
    }

    [Fact]
    public async Task RejectsDuplicateAddressMissingCategoryAndDestructiveOverviewChanges()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        await PhotographyPageSeeder.SeedAsync(fixture.Context);
        var service = new PhotographyPageService(fixture.Context);
        Assert.Equal(409, (await service.SaveAsync(null, new PhotographyPageInput { Slug = "SVATBEN-FOTOGRAF-RUSE", Title = "Test" })).StatusCode);
        Assert.Equal(400, (await service.SaveAsync(null, new PhotographyPageInput { Slug = "valid", Title = "Test", PortfolioCategoryId = 999 })).StatusCode);
        var overview = await fixture.Context.PhotographyPages.SingleAsync(x => x.Slug == "");
        Assert.Equal(400, (await service.DeleteAsync(overview.Id)).StatusCode);
        Assert.Equal(400, (await service.SaveAsync(overview.Id, new PhotographyPageInput { Slug = "changed", Title = "Test" })).StatusCode);
        Assert.Equal(400, (await service.SaveAsync(overview.Id, new PhotographyPageInput { Title = "Test", IsActive = false })).StatusCode);
        Assert.Equal(200, (await service.SaveAsync(overview.Id, new PhotographyPageInput { Title = "Обновено начало на услугите" })).StatusCode);
    }

    [Fact]
    public async Task PreviewsOnlyPublishedDuePublicAlbumsFromLinkedActiveCategory()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var db = fixture.Context;
        var category = new PortfolioCategory { Key = "wedding", Name = "Wedding" };
        var other = new PortfolioCategory { Key = "portrait", Name = "Portrait" };
        db.PortfolioCategories.AddRange(category, other);
        await db.SaveChangesAsync();
        var page = new PhotographyPage { Slug = "wedding", Title = "Wedding", PortfolioCategoryId = category.Id };
        db.PhotographyPages.Add(page);
        var visible = new PortfolioAlbum { Title = "Visible", Slug = "visible", PortfolioCategoryId = category.Id };
        visible.Images.Add(new PortfolioImage { ImageUrl = "/public.jpg", IsPublished = true });
        visible.Images.Add(new PortfolioImage { ImageUrl = "/private.jpg", IsPublished = false, IsCover = true });
        db.PortfolioAlbums.AddRange(visible,
            new PortfolioAlbum { Title = "Other", Slug = "other", PortfolioCategoryId = other.Id },
            new PortfolioAlbum { Title = "Draft", Slug = "draft", PortfolioCategoryId = category.Id, IsPublished = false },
            new PortfolioAlbum { Title = "Future", Slug = "future", PortfolioCategoryId = category.Id, PublishAtUtc = DateTime.UtcNow.AddDays(1) },
            new PortfolioAlbum { Title = "Deleted", Slug = "deleted", PortfolioCategoryId = category.Id, IsDeleted = true },
            new PortfolioAlbum { Title = "User", Slug = "user", PortfolioCategoryId = category.Id, IsUserUploaded = true });
        await db.SaveChangesAsync();
        var service = new PhotographyPageService(db);
        var rows = JsonSerializer.SerializeToElement((await service.AlbumsAsync("wedding")).Value);
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("visible", rows[0].GetProperty("Slug").GetString());
        Assert.Equal("/public.jpg", rows[0].GetProperty("CoverImageUrl").GetString());
        category.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(0, JsonSerializer.SerializeToElement((await service.AlbumsAsync("wedding")).Value).GetArrayLength());
        category.IsActive = true; page.PortfolioCategoryId = null; await db.SaveChangesAsync();
        Assert.Equal(0, JsonSerializer.SerializeToElement((await service.AlbumsAsync("wedding")).Value).GetArrayLength());
    }
}
