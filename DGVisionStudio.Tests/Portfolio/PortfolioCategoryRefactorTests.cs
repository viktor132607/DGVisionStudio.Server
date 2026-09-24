using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioCategoryRefactorTests
{
    [Fact]
    public async Task QueryService_ReturnsOrderedCategoriesAndNotFoundForMissingId()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var second = Category("second", 2);
        var first = Category("first", 1);
        fixture.Context.AddRange(second, first);
        await fixture.Context.SaveChangesAsync();
        var service = new PortfolioCategoryQueryService(fixture.Context);

        var all = await service.GetCategoriesAsync();
        var missing = await service.GetCategoryByIdAsync(9999);

        all.StatusCode.Should().Be(StatusCodes.Status200OK);
        all.Value.Should().BeOfType<List<PortfolioCategory>>()
            .Which.Select(x => x.Id).Should().Equal(first.Id, second.Id);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task OrderingService_ClampsPositionAndNormalizesEveryDisplayOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Category("first", 9);
        var second = Category("second", 20);
        fixture.Context.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var auditService = new PortfolioCategoryAuditService(audit);
        var service = new PortfolioCategoryOrderingService(
            fixture.Context,
            auditService,
            NullLogger<PortfolioCategoryOrderingService>.Instance);

        var moved = await service.MoveCategoryAsync(
            second.Id,
            new DGVisionStudio.Infrastructure.DTOs.MovePortfolioCategoryRequest { DisplayOrder = 0 },
            Context());

        moved.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await fixture.Context.PortfolioCategories.OrderBy(x => x.DisplayOrder).ToListAsync();
        stored.Select(x => x.Id).Should().Equal(second.Id, first.Id);
        stored.Select(x => x.DisplayOrder).Should().Equal(1, 2);
        audit.Entries.Should().ContainSingle(x => x.Action == "MovePortfolioCategory");
    }

    [Fact]
    public async Task AlbumAssignmentService_DoesNotMoveUserUploadsAndAuditsSelection()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var target = Category("target", 1);
        var source = Category("source", 2);
        var regular = Album(source, "regular", false);
        var userUpload = Album(source, "upload", true);
        fixture.Context.AddRange(target, source, regular, userUpload);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new PortfolioCategoryAlbumAssignmentService(
            fixture.Context,
            new PortfolioCategoryAuditService(audit),
            NullLogger<PortfolioCategoryAlbumAssignmentService>.Instance);

        var result = await service.UpdateCategoryAlbumsAsync(
            target.Id,
            new DGVisionStudio.Infrastructure.DTOs.UpdateCategoryAlbumsRequest
            {
                AlbumIds = [regular.Id, userUpload.Id]
            },
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        regular.PortfolioCategoryId.Should().Be(target.Id);
        userUpload.PortfolioCategoryId.Should().Be(source.Id);
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioCategoryAlbums");
    }

    [Fact]
    public async Task FacadeConstructor_DelegatesAllCategoryResponsibilitiesToFocusedServices()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var audit = new RecordingAuditLogService();
        var auditService = new PortfolioCategoryAuditService(audit);
        var ordering = new PortfolioCategoryOrderingService(
            fixture.Context,
            auditService,
            NullLogger<PortfolioCategoryOrderingService>.Instance);
        var facade = new PortfolioCategoryAdminService(
            new PortfolioCategoryQueryService(fixture.Context),
            new PortfolioCategoryCommandService(
                fixture.Context,
                auditService,
                ordering,
                NullLogger<PortfolioCategoryCommandService>.Instance),
            ordering,
            new PortfolioCategoryAlbumAssignmentService(
                fixture.Context,
                auditService,
                NullLogger<PortfolioCategoryAlbumAssignmentService>.Instance));

        var created = await facade.CreateCategoryAsync(
            new DGVisionStudio.Infrastructure.DTOs.CreatePortfolioCategoryRequest
            {
                Key = " portraits ",
                Name = " Портрети ",
                NameEn = " Portraits ",
                IsActive = true
            },
            Context());
        var listed = await facade.GetCategoriesAsync();

        created.StatusCode.Should().Be(StatusCodes.Status200OK);
        listed.Value.Should().BeOfType<List<PortfolioCategory>>()
            .Which.Should().ContainSingle(x => x.Key == "portraits");
    }

    private static PortfolioCategory Category(string key, int displayOrder) => new()
    {
        Key = key,
        Name = key,
        NameEn = key,
        DisplayOrder = displayOrder,
        IsActive = true
    };

    private static PortfolioAlbum Album(PortfolioCategory category, string slug, bool userUploaded) => new()
    {
        PortfolioCategory = category,
        Slug = slug,
        Title = slug,
        IsPublished = true,
        AllowClientAccess = true,
        IsUserUploaded = userUploaded
    };

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace");
}
