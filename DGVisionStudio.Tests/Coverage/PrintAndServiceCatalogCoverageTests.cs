using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CatalogService = DGVisionStudio.Domain.Entities.Service;

namespace DGVisionStudio.Tests.Coverage;

public sealed class AdminPrintRequestCommandAdditionalTests
{
    [Theory]
    [InlineData("New", UserClientGalleryStatus.Pending)]
    [InlineData("InProgress", UserClientGalleryStatus.PrintInProgress)]
    [InlineData("Completed", UserClientGalleryStatus.Processed)]
    [InlineData("Cancelled", UserClientGalleryStatus.Expired)]
    public async Task UpdateStatusAsync_MapsEveryAlbumStatus(string status, UserClientGalleryStatus expected)
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var album = AddAlbum(fixture.Context, clientUpload: true, seen: false);
        await fixture.Context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(fixture.Context);

        var result = await service.UpdateStatusAsync(
            -album.Id,
            new UpdatePrintRequestStatusDto { Status = status });

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        album.UserGalleryStatus.Should().Be(expected);
        album.IsSeenByAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task MarkSeenAsync_HandlesDirectAndAlbumRequestsAndMissingIds()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("client@example.com", "client");
        var requestAlbum = AddAlbum(fixture.Context, clientUpload: false, seen: true);
        var request = Request(user, requestAlbum, "client@example.com", seen: false);
        var uploadAlbum = AddAlbum(fixture.Context, clientUpload: true, seen: false);
        fixture.Context.AddRange(user, request);
        await fixture.Context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(fixture.Context);

        var direct = await service.MarkSeenAsync(request.Id);
        var upload = await service.MarkSeenAsync(-uploadAlbum.Id);
        var missingDirect = await service.MarkSeenAsync(999999);
        var missingUpload = await service.MarkSeenAsync(-999999);

        direct.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        upload.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        request.IsSeenByAdmin.Should().BeTrue();
        request.UpdatedAtUtc.Should().NotBeNull();
        uploadAlbum.IsSeenByAdmin.Should().BeTrue();
        missingDirect.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        missingUpload.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task MarkAllSeenAsync_UpdatesOnlyUnseenRequestsAndEligibleUploadAlbums()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("client@example.com", "client");
        var requestAlbum = AddAlbum(fixture.Context, clientUpload: false, seen: true);
        var unseen = Request(user, requestAlbum, "unseen@example.com", seen: false);
        var alreadySeen = Request(user, requestAlbum, "seen@example.com", seen: true);
        var eligible = AddAlbum(fixture.Context, clientUpload: true, seen: false);
        var deleted = AddAlbum(fixture.Context, clientUpload: true, seen: false);
        deleted.IsDeleted = true;
        var ordinary = AddAlbum(fixture.Context, clientUpload: false, seen: false);
        fixture.Context.AddRange(user, unseen, alreadySeen);
        await fixture.Context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(fixture.Context);

        var result = await service.MarkAllSeenAsync();

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        unseen.IsSeenByAdmin.Should().BeTrue();
        unseen.UpdatedAtUtc.Should().NotBeNull();
        alreadySeen.IsSeenByAdmin.Should().BeTrue();
        eligible.IsSeenByAdmin.Should().BeTrue();
        deleted.IsSeenByAdmin.Should().BeFalse();
        ordinary.IsSeenByAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesDirectRequestAndReturnsNotFoundForSecondDelete()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("client@example.com", "client");
        var requestAlbum = AddAlbum(fixture.Context, clientUpload: false, seen: true);
        var request = Request(user, requestAlbum, "request@example.com", seen: false);
        fixture.Context.AddRange(user, request);
        await fixture.Context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(fixture.Context);

        var deleted = await service.DeleteAsync(request.Id);
        var missing = await service.DeleteAsync(request.Id);

        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await fixture.Context.PrintRequests.CountAsync()).Should().Be(0);
    }

    private static PrintRequest Request(
        ApplicationUser user,
        PortfolioAlbum album,
        string email,
        bool seen) => new()
        {
            User = user,
            UserId = user.Id,
            PortfolioAlbum = album,
            FullName = "Client",
            Email = email,
            Status = "New",
            IsSeenByAdmin = seen
        };

    private static PortfolioAlbum AddAlbum(
        DGVisionStudio.Infrastructure.Data.AppDbContext context,
        bool clientUpload,
        bool seen)
    {
        var category = new PortfolioCategory
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = "Category",
            NameEn = "Category",
            IsActive = true
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = Guid.NewGuid().ToString("N"),
            Title = clientUpload ? "Client upload" : "Portfolio album",
            GalleryType = clientUpload ? GalleryType.ClientPrintUpload : default,
            IsUserUploaded = clientUpload,
            IsSeenByAdmin = seen,
            AllowClientAccess = true,
            IsPublished = true
        };
        context.AddRange(category, album);
        return album;
    }
}

public sealed class ServiceCatalogAdditionalTests
{
    [Fact]
    public async Task GetMethods_ReturnOrderedCollectionsAndExpectedNotFoundResults()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var inactive = Item("Inactive", 1, active: false);
        var activeSecond = Item("Second", 3, active: true);
        var activeFirst = Item("First", 2, active: true);
        fixture.Context.Services.AddRange(inactive, activeSecond, activeFirst);
        await fixture.Context.SaveChangesAsync();
        var service = new ServiceCatalogService(fixture.Context);

        var active = await service.GetActiveAsync();
        var all = await service.GetAllAsync();
        var found = await service.GetAsync(activeFirst.Id);
        var publicFound = await service.GetPublicByIdAsync(inactive.Id);
        var missing = await service.GetAsync(999999);
        var publicMissing = await service.GetPublicByIdAsync(999999);

        active.Value.Should().BeOfType<List<CatalogService>>()
            .Which.Select(x => x.Title).Should().Equal("First", "Second");
        all.Value.Should().BeOfType<List<CatalogService>>()
            .Which.Select(x => x.Title).Should().Equal("Inactive", "First", "Second");
        found.StatusCode.Should().Be(StatusCodes.Status200OK);
        publicFound.StatusCode.Should().Be(StatusCodes.Status200OK);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        publicMissing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateAsync_HandlesMissingValidationAndNormalizedSuccessfulUpdate()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var item = Item("Old", 1, active: false);
        fixture.Context.Services.Add(item);
        await fixture.Context.SaveChangesAsync();
        var service = new ServiceCatalogService(fixture.Context);

        var missing = await service.UpdateAsync(999999, ValidDto());
        var invalidTitle = await service.UpdateAsync(item.Id, new ServiceCardDto
        {
            Title = " ",
            Description = "Description"
        });
        var invalidDescription = await service.UpdateAsync(item.Id, new ServiceCardDto
        {
            Title = "Title",
            Description = " "
        });
        var updated = await service.UpdateAsync(item.Id, new ServiceCardDto
        {
            Title = "  Updated  ",
            ShortDescription = "  Short  ",
            Description = "  Full description  ",
            CoverImageUrl = "  /cover.jpg  ",
            IsActive = true
        });

        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        invalidTitle.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        invalidDescription.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        updated.StatusCode.Should().Be(StatusCodes.Status200OK);
        item.Title.Should().Be("Updated");
        item.ShortDescription.Should().Be("Short");
        item.Description.Should().Be("Full description");
        item.CoverImageUrl.Should().Be("/cover.jpg");
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderAsync_RejectsEmptyInputAndHandlesDuplicatesUnknownIdsAndRemainingItems()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Item("First", 1, active: true);
        var second = Item("Second", 2, active: true);
        var third = Item("Third", 3, active: true);
        fixture.Context.Services.AddRange(first, second, third);
        await fixture.Context.SaveChangesAsync();
        var service = new ServiceCatalogService(fixture.Context);

        var empty = await service.ReorderAsync(new ReorderServicesDto());
        var reordered = await service.ReorderAsync(new ReorderServicesDto
        {
            Ids = [third.Id, third.Id, 999999, first.Id]
        });

        empty.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        reordered.StatusCode.Should().Be(StatusCodes.Status200OK);
        var items = reordered.Value.Should().BeOfType<List<CatalogService>>().Subject;
        items.Select(x => x.Id).Should().Equal(third.Id, first.Id, second.Id);
        items.Select(x => x.DisplayOrder).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItemNormalizesRemainingOrderAndHandlesMissingId()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Item("First", 4, active: true);
        var second = Item("Second", 8, active: true);
        var third = Item("Third", 12, active: true);
        fixture.Context.Services.AddRange(first, second, third);
        await fixture.Context.SaveChangesAsync();
        var service = new ServiceCatalogService(fixture.Context);

        var deleted = await service.DeleteAsync(second.Id);
        var missing = await service.DeleteAsync(second.Id);

        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var remaining = await fixture.Context.Services.OrderBy(x => x.DisplayOrder).ToListAsync();
        remaining.Select(x => x.Id).Should().Equal(first.Id, third.Id);
        remaining.Select(x => x.DisplayOrder).Should().Equal(1, 2);
    }

    private static CatalogService Item(string title, int order, bool active) => new()
    {
        Title = title,
        Description = $"{title} description",
        DisplayOrder = order,
        IsActive = active
    };

    private static ServiceCardDto ValidDto() => new()
    {
        Title = "Title",
        Description = "Description",
        IsActive = true
    };
}
