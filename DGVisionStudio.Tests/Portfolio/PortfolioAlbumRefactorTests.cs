using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioAlbumRefactorTests
{
    [Fact]
    public void InputNormalizer_TrimsAndNullsOptionalFields()
    {
        var input = PortfolioAlbumInputNormalizer.Normalize(
            new CreatePortfolioAlbumRequest
            {
                PortfolioCategoryId = 5,
                Slug = "  album-slug  ",
                Title = "  Албум  ",
                TitleEn = "  Album  ",
                Description = " ",
                CoverImageUrl = "  /cover.jpg  ",
                DisplayOrder = 7,
                ColumnNumber = 2,
                IsPublished = true
            });

        input.PortfolioCategoryId.Should().Be(5);
        input.Slug.Should().Be("album-slug");
        input.Title.Should().Be("Албум");
        input.TitleEn.Should().Be("Album");
        input.Description.Should().BeNull();
        input.CoverImageUrl.Should().Be("/cover.jpg");
        input.DisplayOrder.Should().Be(7);
        input.ColumnNumber.Should().Be(2);
        input.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_RejectsInvalidCategoryBlankFieldsAndDuplicateSlug()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category("valid");
        fixture.Context.PortfolioCategories.Add(category);
        await fixture.Context.SaveChangesAsync();

        var validator = new PortfolioAlbumInputValidator(fixture.Context);

        var invalidCategory = await validator.ValidateAsync(
            Input(999, "slug", "Title"));
        var blankSlug = await validator.ValidateAsync(
            Input(category.Id, " ", "Title"));
        var blankTitle = await validator.ValidateAsync(
            Input(category.Id, "slug", " "));

        fixture.Context.PortfolioAlbums.Add(new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "duplicate",
            Title = "Existing"
        });
        await fixture.Context.SaveChangesAsync();

        var duplicate = await validator.ValidateAsync(
            Input(category.Id, "duplicate", "Title"));

        invalidCategory!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        blankSlug!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        blankTitle!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        duplicate!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task QueryService_ReturnsOnlyAdminAlbumsWithPaging()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category("gallery");
        fixture.Context.AddRange(
            Album(category, "first", 1, false),
            Album(category, "second", 2, false),
            Album(category, "user", 3, true));
        await fixture.Context.SaveChangesAsync();

        var result = await new PortfolioAlbumQueryService(fixture.Context)
            .GetAlbumsAsync(new PagedQueryDto { Page = 1, PageSize = 1 });

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var page = result.Value.Should()
            .BeOfType<PagedResultDto<PortfolioAlbum>>()
            .Subject;

        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle(x => !x.IsUserUploaded);
    }

    [Fact]
    public async Task CommandService_UpdatesAndSoftDeletesAlbumWithImagesAndAudit()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var source = Category("source");
        var target = Category("target");
        var album = Album(source, "album", 1, false);
        album.IsPublished = true;
        album.AllowClientAccess = true;
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/photo.jpg",
            IsPublished = true,
            IsCover = true
        });

        fixture.Context.AddRange(source, target, album);
        await fixture.Context.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var command = CreateCommand(fixture, audit);

        var updated = await command.UpdateAlbumAsync(
            album.Id,
            new UpdatePortfolioAlbumRequest
            {
                PortfolioCategoryId = target.Id,
                Slug = "  updated  ",
                Title = "  Updated title  ",
                TitleEn = "  Updated  ",
                Description = " ",
                CoverImageUrl = " /new-cover.jpg ",
                DisplayOrder = 4,
                ColumnNumber = 2,
                IsPublished = true
            },
            PortfolioAdminTestContext.Create());

        updated.StatusCode.Should().Be(StatusCodes.Status200OK);
        album.PortfolioCategoryId.Should().Be(target.Id);
        album.Slug.Should().Be("updated");
        album.Description.Should().BeNull();
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioAlbum");

        var deleted = await command.DeleteAlbumAsync(
            album.Id,
            PortfolioAdminTestContext.Create());

        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        album.IsDeleted.Should().BeTrue();
        album.IsPublished.Should().BeFalse();
        album.AllowClientAccess.Should().BeFalse();
        album.Images.Should().OnlyContain(
            x => x.IsDeleted && !x.IsPublished && !x.IsCover && x.DeletedAtUtc != null);
        audit.Entries.Should().Contain(x => x.Action == "SoftDeletePortfolioAlbum");
    }

    [Fact]
    public async Task Facade_DelegatesToFocusedQueryAndCommandServices()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category("portfolio");
        fixture.Context.PortfolioCategories.Add(category);
        await fixture.Context.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var command = CreateCommand(fixture, audit);
        var facade = new PortfolioAlbumAdminService(
            new PortfolioAlbumQueryService(fixture.Context),
            command);

        var created = await facade.CreateAlbumAsync(
            new CreatePortfolioAlbumRequest
            {
                PortfolioCategoryId = category.Id,
                Slug = "album",
                Title = "Албум"
            },
            PortfolioAdminTestContext.Create());

        var listed = await facade.GetAlbumsAsync(new PagedQueryDto());

        created.StatusCode.Should().Be(StatusCodes.Status200OK);
        listed.Value.Should().BeOfType<PagedResultDto<PortfolioAlbum>>()
            .Which.Total.Should().Be(1);
    }

    private static PortfolioAlbumCommandService CreateCommand(
        GallerySqliteFixture fixture,
        RecordingAuditLogService audit) =>
        new(
            fixture.Context,
            new PortfolioAlbumInputValidator(fixture.Context),
            new PortfolioAlbumMapper(),
            new PortfolioAlbumAuditService(audit),
            NullLogger<PortfolioAlbumCommandService>.Instance);

    private static PortfolioAlbumInput Input(
        int categoryId,
        string slug,
        string title) =>
        new(categoryId, slug, title, null, null, null, 0, null, true);

    private static PortfolioCategory Category(string key) => new()
    {
        Key = key,
        Name = key,
        NameEn = key,
        IsActive = true
    };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug,
        int displayOrder,
        bool userUploaded) =>
        new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            DisplayOrder = displayOrder,
            IsUserUploaded = userUploaded
        };
}
