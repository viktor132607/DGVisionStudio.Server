using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Admin;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioCategoryAdminServiceTests
{
    [Fact]
    public async Task CreateCategoryAsync_ValidatesAndPersistsNormalizedCategoryWithAudit()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var audit = new RecordingAuditLogService();
        var service = new PortfolioCategoryAdminService(
            fixture.Context,
            audit,
            NullLogger<PortfolioCategoryAdminService>.Instance);

        var invalid = await service.CreateCategoryAsync(
            new CreatePortfolioCategoryRequest { Key = "portraits", Name = " ", NameEn = "Portraits" },
            PortfolioAdminTestContext.Create());
        var valid = await service.CreateCategoryAsync(
            new CreatePortfolioCategoryRequest
            {
                Key = "  PORTRAITS  ",
                Name = "  Портрети  ",
                NameEn = "  Portraits  ",
                DisplayOrder = 0,
                IsActive = true
            },
            PortfolioAdminTestContext.Create());

        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        valid.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await fixture.Context.PortfolioCategories.SingleAsync();
        stored.Key.Should().Be("portraits");
        stored.Name.Should().Be("Портрети");
        stored.NameEn.Should().Be("Portraits");
        stored.DisplayOrder.Should().Be(1);
        audit.Entries.Should().ContainSingle(x => x.Action == "CreatePortfolioCategory");
    }
}

public sealed class PortfolioAlbumAdminServiceTests
{
    [Fact]
    public async Task CreateAlbumAsync_RejectsMissingCategoryThenCreatesAlbumWithAudit()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var audit = new RecordingAuditLogService();
        var service = new PortfolioAlbumAdminService(
            fixture.Context,
            audit,
            NullLogger<PortfolioAlbumAdminService>.Instance);

        var invalid = await service.CreateAlbumAsync(
            new CreatePortfolioAlbumRequest
            {
                PortfolioCategoryId = 999,
                Slug = "album",
                Title = "Албум"
            },
            PortfolioAdminTestContext.Create());
        var category = new PortfolioCategory
        {
            Key = "weddings",
            Name = "Сватби",
            NameEn = "Weddings"
        };
        fixture.Context.PortfolioCategories.Add(category);
        await fixture.Context.SaveChangesAsync();
        var valid = await service.CreateAlbumAsync(
            new CreatePortfolioAlbumRequest
            {
                PortfolioCategoryId = category.Id,
                Slug = "  wedding-day  ",
                Title = "  Сватбен ден  ",
                TitleEn = "  Wedding day  ",
                DisplayOrder = 2,
                ColumnNumber = 1,
                IsPublished = true
            },
            PortfolioAdminTestContext.Create());

        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        valid.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await fixture.Context.PortfolioAlbums.SingleAsync();
        stored.Slug.Should().Be("wedding-day");
        stored.Title.Should().Be("Сватбен ден");
        stored.IsUserUploaded.Should().BeFalse();
        audit.Entries.Should().ContainSingle(x => x.Action == "CreatePortfolioAlbum");
    }
}

public sealed class PortfolioImageAdminServiceTests
{
    [Fact]
    public async Task CreateImageAsync_RejectsMissingAlbumThenCreatesImageWithAudit()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var audit = new RecordingAuditLogService();
        var service = new PortfolioImageAdminService(
            fixture.Context,
            audit,
            NullLogger<PortfolioImageAdminService>.Instance);

        var invalid = await service.CreateImageAsync(
            new CreatePortfolioImageRequest { PortfolioAlbumId = 999, ImageUrl = "/missing.jpg" },
            PortfolioAdminTestContext.Create());
        var category = new PortfolioCategory { Key = "events", Name = "Събития", NameEn = "Events" };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "event",
            Title = "Събитие",
            IsUserUploaded = false
        };
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();
        var valid = await service.CreateImageAsync(
            new CreatePortfolioImageRequest
            {
                PortfolioAlbumId = album.Id,
                ImageUrl = "  /uploads/event.jpg  ",
                AltText = "  Event image  ",
                DisplayOrder = 1,
                IsPublished = true
            },
            PortfolioAdminTestContext.Create());

        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        valid.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await fixture.Context.PortfolioImages.SingleAsync();
        stored.ImageUrl.Should().Be("/uploads/event.jpg");
        stored.AltText.Should().Be("Event image");
        audit.Entries.Should().ContainSingle(x => x.Action == "CreatePortfolioImage");
    }
}

public sealed class AdminPortfolioServiceTests
{
    [Fact]
    public async Task GetCategoriesAsync_DelegatesToCategoryService()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.PortfolioCategories.Add(new PortfolioCategory
        {
            Key = "portraits",
            Name = "Портрети",
            NameEn = "Portraits"
        });
        await context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new AdminPortfolioService(
            new PortfolioCategoryAdminService(context, audit, NullLogger<PortfolioCategoryAdminService>.Instance),
            new PortfolioAlbumAdminService(context, audit, NullLogger<PortfolioAlbumAdminService>.Instance),
            new PortfolioImageAdminService(context, audit, NullLogger<PortfolioImageAdminService>.Instance));

        var result = await service.GetCategoriesAsync();

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<List<PortfolioCategory>>()
            .Which.Should().ContainSingle(x => x.Key == "portraits");
    }
}

public sealed class PortfolioQueryServiceTests
{
    [Fact]
    public async Task GetAlbumsAsync_ReturnsOnlyPublishedNonUserAlbumsFromActiveCategories()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var active = new PortfolioCategory { Key = "active", Name = "Active", NameEn = "Active", IsActive = true };
        var inactive = new PortfolioCategory { Key = "inactive", Name = "Inactive", NameEn = "Inactive", IsActive = false };
        context.AddRange(
            active,
            inactive,
            Album(active, "visible", published: true, userUploaded: false),
            Album(active, "draft", published: false, userUploaded: false),
            Album(active, "client-upload", published: true, userUploaded: true),
            Album(inactive, "hidden-category", published: true, userUploaded: false));
        await context.SaveChangesAsync();
        var service = new PortfolioQueryService(context);

        var result = await service.GetAlbumsAsync(null);

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<List<PortfolioAlbum>>()
            .Which.Should().ContainSingle(x => x.Slug == "visible");
    }

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string slug,
        bool published,
        bool userUploaded) => new()
        {
            PortfolioCategory = category,
            Slug = slug,
            Title = slug,
            IsPublished = published,
            IsUserUploaded = userUploaded
        };
}

public sealed class AdminUserServiceTests
{
    [Fact]
    public async Task BlockUserAsync_ProtectsConfiguredAdminAccount()
    {
        var user = TestUsers.Create("protected@example.com", "admin-1");
        var manager = new ConfigurableUserManager([user]);
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminUserService(
            manager,
            context,
            TestConfiguration.Create(("Seed:PrimaryAdminEmail", user.Email)));

        var result = await service.BlockUserAsync(user.Id);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        user.IsBlocked.Should().BeFalse();
    }
}

public sealed class ServiceCatalogServiceTests
{
    [Fact]
    public async Task CreateAndReorder_ValidatesAndNormalizesServiceOrder()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new ServiceCatalogService(context);

        var invalid = await service.CreateAsync(new ServiceCardDto { Title = " ", Description = "Description" });
        var first = await service.CreateAsync(new ServiceCardDto
        {
            Title = "  Portraits  ",
            Description = "  Portrait sessions  ",
            IsActive = true
        });
        var second = await service.CreateAsync(new ServiceCardDto
        {
            Title = "Weddings",
            Description = "Wedding photography",
            IsActive = true
        });
        var firstId = ((DGVisionStudio.Domain.Entities.Service)first.Value!).Id;
        var secondId = ((DGVisionStudio.Domain.Entities.Service)second.Value!).Id;
        var reordered = await service.ReorderAsync(new ReorderServicesDto { Ids = [secondId, firstId] });

        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        first.StatusCode.Should().Be(StatusCodes.Status201Created);
        reordered.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await context.Services.OrderBy(x => x.DisplayOrder).ToListAsync();
        stored.Select(x => x.Id).Should().Equal(secondId, firstId);
        stored.First(x => x.Id == firstId).Title.Should().Be("Portraits");
    }
}

public sealed class AdminStatisticsServiceTests
{
    [Fact]
    public async Task GetNotificationCountsAsync_AggregatesAllUnseenSources()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("new@example.com", "user-1");
        user.IsSeenByAdmin = false;
        user.IsBlocked = false;
        context.Users.Add(user);
        context.ContactRequests.Add(new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Client",
            Email = "client@example.com",
            Phone = "+359888123456",
            Message = "Request",
            IsSeenByAdmin = false,
            IsArchived = false
        });
        context.PrintRequests.Add(new PrintRequest
        {
            UserId = user.Id,
            FullName = "Client",
            Email = "client@example.com",
            IsSeenByAdmin = false
        });
        context.PortfolioAlbums.Add(new PortfolioAlbum
        {
            Slug = "upload",
            Title = "Upload",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            IsSeenByAdmin = false,
            IsDeleted = false
        });
        await context.SaveChangesAsync();
        var manager = new ConfigurableUserManager([user]) { UsersSource = context.Users };
        var service = new AdminStatisticsService(context, manager);

        var result = await service.GetNotificationCountsAsync();

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var counts = result.Value.Should().BeOfType<AdminNotificationCountsDto>().Subject;
        counts.NewUsers.Should().Be(1);
        counts.NewContactRequests.Should().Be(1);
        counts.NewPrintRequests.Should().Be(2);
    }
}

internal static class PortfolioAdminTestContext
{
    public static AdminRequestContext Create() => new(
        "admin-1",
        "admin@example.com",
        "Admin",
        "127.0.0.1",
        "tests",
        "trace-1");
}
