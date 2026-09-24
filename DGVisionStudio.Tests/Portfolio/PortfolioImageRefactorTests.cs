using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.DTOs;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioImageRefactorTests
{
    [Fact]
    public void InputNormalizer_TrimsAndNormalizesNullableFieldsAndDimensions()
    {
        var input = PortfolioImageInputNormalizer.Normalize(
            new CreatePortfolioImageRequest
            {
                PortfolioAlbumId = 7,
                ImageUrl = "  /photo.jpg  ",
                ThumbnailUrl = "  /thumb.jpg  ",
                AltText = "  Alt  ",
                Caption = " ",
                Width = null,
                Height = 900,
                DisplayOrder = 3,
                IsCover = true,
                IsPublished = true
            });

        input.PortfolioAlbumId.Should().Be(7);
        input.ImageUrl.Should().Be("/photo.jpg");
        input.ThumbnailUrl.Should().Be("/thumb.jpg");
        input.AltText.Should().Be("Alt");
        input.Caption.Should().BeNull();
        input.Width.Should().Be(0);
        input.Height.Should().Be(900);
        input.DisplayOrder.Should().Be(3);
        input.IsCover.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_RejectsMissingOrUserUploadedAlbumAndBlankUrl()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category();
        var normalAlbum = Album(category, "normal", false);
        var userAlbum = Album(category, "user", true);
        fixture.Context.AddRange(category, normalAlbum, userAlbum);
        await fixture.Context.SaveChangesAsync();

        var validator = new PortfolioImageInputValidator(fixture.Context);

        var missing = await validator.ValidateAsync(Input(999, "/photo.jpg"));
        var user = await validator.ValidateAsync(Input(userAlbum.Id, "/photo.jpg"));
        var blank = await validator.ValidateAsync(Input(normalAlbum.Id, " "));

        missing!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        user!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        blank!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task QueryService_ReturnsOnlyImagesFromAdminAlbumsWithPaging()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category();
        var normalAlbum = Album(category, "normal", false);
        var userAlbum = Album(category, "user", true);

        normalAlbum.Images.Add(Image("/one.jpg", 1));
        normalAlbum.Images.Add(Image("/two.jpg", 2));
        userAlbum.Images.Add(Image("/user.jpg", 1));

        fixture.Context.AddRange(category, normalAlbum, userAlbum);
        await fixture.Context.SaveChangesAsync();

        var result = await new PortfolioImageQueryService(fixture.Context)
            .GetImagesAsync(new PagedQueryDto { Page = 1, PageSize = 1 });

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var page = result.Value.Should()
            .BeOfType<PagedResultDto<PortfolioImage>>()
            .Subject;

        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();
        page.Items.Should().OnlyContain(x => x.PortfolioAlbumId == normalAlbum.Id);
    }

    [Fact]
    public async Task CommandService_UpdatesAndSoftDeletesImageWithAudit()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category();
        var firstAlbum = Album(category, "first", false);
        var secondAlbum = Album(category, "second", false);
        var image = Image("/old.jpg", 1);
        image.IsCover = true;
        image.IsPublished = true;
        firstAlbum.Images.Add(image);

        fixture.Context.AddRange(category, firstAlbum, secondAlbum);
        await fixture.Context.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var command = CreateCommand(fixture, audit);

        var updated = await command.UpdateImageAsync(
            image.Id,
            new UpdatePortfolioImageRequest
            {
                PortfolioAlbumId = secondAlbum.Id,
                ImageUrl = "  /new.jpg  ",
                ThumbnailUrl = " /thumb.jpg ",
                AltText = " New alt ",
                Caption = " ",
                Width = 1600,
                Height = 900,
                DisplayOrder = 4,
                IsCover = true,
                IsPublished = true
            },
            PortfolioAdminTestContext.Create());

        updated.StatusCode.Should().Be(StatusCodes.Status200OK);
        image.PortfolioAlbumId.Should().Be(secondAlbum.Id);
        image.ImageUrl.Should().Be("/new.jpg");
        image.Caption.Should().BeNull();
        image.Width.Should().Be(1600);
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioImage");

        var deleted = await command.DeleteImageAsync(
            image.Id,
            PortfolioAdminTestContext.Create());

        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        image.IsDeleted.Should().BeTrue();
        image.IsPublished.Should().BeFalse();
        image.IsCover.Should().BeFalse();
        image.DeletedAtUtc.Should().NotBeNull();
        audit.Entries.Should().Contain(x => x.Action == "SoftDeletePortfolioImage");
    }

    [Fact]
    public async Task Facade_DelegatesFocusedQueryAndCommandServices()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category();
        var album = Album(category, "portfolio", false);
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var facade = new PortfolioImageAdminService(
            new PortfolioImageQueryService(fixture.Context),
            CreateCommand(fixture, audit));

        var created = await facade.CreateImageAsync(
            new CreatePortfolioImageRequest
            {
                PortfolioAlbumId = album.Id,
                ImageUrl = "/created.jpg"
            },
            PortfolioAdminTestContext.Create());

        var listed = await facade.GetImagesAsync(new PagedQueryDto());

        created.StatusCode.Should().Be(StatusCodes.Status200OK);
        listed.Value.Should()
            .BeOfType<PagedResultDto<PortfolioImage>>()
            .Which.Total.Should().Be(1);
    }

    private static PortfolioImageCommandService CreateCommand(
        GallerySqliteFixture fixture,
        RecordingAuditLogService audit) =>
        new(
            fixture.Context,
            new PortfolioImageInputValidator(fixture.Context),
            new PortfolioImageMapper(),
            new PortfolioImageAuditService(audit),
            NullLogger<PortfolioImageCommandService>.Instance);

    private static PortfolioImageInput Input(
        int albumId,
        string imageUrl) =>
        new(
            albumId,
            imageUrl,
            null,
            null,
            null,
            0,
            0,
            0,
            false,
            true);

    private static PortfolioCategory Category() =>
        new()
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = "Portfolio",
            NameEn = "Portfolio",
            IsActive = true
        };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug,
        bool userUploaded) =>
        new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            IsUserUploaded = userUploaded
        };

    private static PortfolioImage Image(
        string imageUrl,
        int displayOrder) =>
        new()
        {
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder,
            IsPublished = true
        };
}
