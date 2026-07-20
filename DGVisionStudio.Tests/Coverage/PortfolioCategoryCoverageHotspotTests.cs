using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Coverage;

public sealed class PortfolioCategoryAdminCoverageTests
{
    [Fact]
    public async Task CreateAndUpdate_RejectDuplicateKeysAndNormalizeSuccessfulUpdate()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var existing = Category("existing", 1);
        var updated = Category("updated", 2);
        fixture.Context.AddRange(existing, updated);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var duplicateCreate = await service.CreateCategoryAsync(
            new CreatePortfolioCategoryRequest
            {
                Key = " EXISTING ",
                Name = "Duplicate",
                NameEn = "Duplicate"
            },
            Context());
        var duplicateUpdate = await service.UpdateCategoryAsync(
            updated.Id,
            new UpdatePortfolioCategoryRequest
            {
                Key = " EXISTING ",
                Name = "Updated",
                NameEn = "Updated"
            },
            Context());
        var validUpdate = await service.UpdateCategoryAsync(
            updated.Id,
            new UpdatePortfolioCategoryRequest
            {
                Key = " NEW-KEY ",
                Name = "  Ново име  ",
                NameEn = "  New name  ",
                Description = "  Description  ",
                DisplayOrder = 0,
                IsActive = true
            },
            Context());

        duplicateCreate.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        duplicateUpdate.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        validUpdate.StatusCode.Should().Be(StatusCodes.Status200OK);
        updated.Key.Should().Be("new-key");
        updated.Name.Should().Be("Ново име");
        updated.NameEn.Should().Be("New name");
        updated.Description.Should().Be("Description");
        updated.DisplayOrder.Should().Be(1);
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioCategory");
    }

    [Fact]
    public async Task MoveCategoryAsync_ReordersEveryCategoryAndAuditsChange()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Category("first", 1);
        var second = Category("second", 2);
        var third = Category("third", 3);
        fixture.Context.AddRange(first, second, third);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var result = await service.MoveCategoryAsync(
            third.Id,
            new MovePortfolioCategoryRequest { DisplayOrder = 1 },
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var ordered = await fixture.Context.PortfolioCategories.OrderBy(x => x.DisplayOrder).ToListAsync();
        ordered.Select(x => x.Id).Should().Equal(third.Id, first.Id, second.Id);
        ordered.Select(x => x.DisplayOrder).Should().Equal(1, 2, 3);
        audit.Entries.Should().ContainSingle(x => x.Action == "MovePortfolioCategory");
    }

    [Fact]
    public async Task CategoryAlbumMethods_ReturnSelectionAndAssignRequestedNonUserAlbums()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category("target", 1);
        var other = Category("other", 2);
        var selected = Album(other, "selected", userUploaded: false);
        var unselected = Album(other, "unselected", userUploaded: false);
        var userUpload = Album(other, "user-upload", userUploaded: true);
        fixture.Context.AddRange(category, other, selected, unselected, userUpload);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var before = await service.GetCategoryAlbumsAsync(category.Id);
        var updated = await service.UpdateCategoryAlbumsAsync(
            category.Id,
            new UpdateCategoryAlbumsRequest { AlbumIds = [selected.Id, selected.Id] },
            Context());
        var missing = await service.GetCategoryAlbumsAsync(9999);

        before.StatusCode.Should().Be(StatusCodes.Status200OK);
        updated.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        selected.PortfolioCategoryId.Should().Be(category.Id);
        unselected.PortfolioCategoryId.Should().Be(other.Id);
        userUpload.PortfolioCategoryId.Should().Be(other.Id);
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioCategoryAlbums");
    }

    [Fact]
    public async Task DeleteCategoryAsync_SoftDeletesCategoryAlbumsAndImagesThenNormalizesOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var deleted = Category("delete", 1);
        var remaining = Category("remaining", 4);
        var album = Album(deleted, "album", userUploaded: false);
        var image = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/photo.jpg",
            IsPublished = true,
            IsCover = true
        };
        fixture.Context.AddRange(deleted, remaining, album, image);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var result = await service.DeleteCategoryAsync(deleted.Id, Context());

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        deleted.IsDeleted.Should().BeTrue();
        deleted.IsActive.Should().BeFalse();
        album.IsDeleted.Should().BeTrue();
        album.IsPublished.Should().BeFalse();
        album.AllowClientAccess.Should().BeFalse();
        image.IsDeleted.Should().BeTrue();
        image.IsPublished.Should().BeFalse();
        image.IsCover.Should().BeFalse();
        remaining.DisplayOrder.Should().Be(1);
        audit.Entries.Should().ContainSingle(x => x.Action == "SoftDeletePortfolioCategory");
    }

    private static PortfolioCategoryAdminService Create(
        DGVisionStudio.Infrastructure.Data.AppDbContext context,
        RecordingAuditLogService audit) =>
        new(context, audit, NullLogger<PortfolioCategoryAdminService>.Instance);

    private static PortfolioCategory Category(string key, int order) => new()
    {
        Key = key,
        Name = key,
        NameEn = key,
        DisplayOrder = order,
        IsActive = true
    };

    private static PortfolioAlbum Album(PortfolioCategory category, string slug, bool userUploaded) => new()
    {
        PortfolioCategory = category,
        Slug = slug,
        Title = slug,
        DisplayOrder = 1,
        IsPublished = true,
        AllowClientAccess = true,
        IsUserUploaded = userUploaded
    };

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace");
}
