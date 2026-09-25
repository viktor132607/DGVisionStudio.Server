using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.PrintRequests;

public sealed class AdminPrintRequestCommandRefactorTests
{
    [Theory]
    [InlineData("New", UserClientGalleryStatus.Pending)]
    [InlineData("InProgress", UserClientGalleryStatus.PrintInProgress)]
    [InlineData("Completed", UserClientGalleryStatus.Processed)]
    [InlineData("Cancelled", UserClientGalleryStatus.Expired)]
    public async Task StatusService_PreservesUploadedAlbumStatusMapping(
        string status,
        UserClientGalleryStatus expected)
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = UploadedAlbum("status-map");
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();

        var service = new AdminPrintRequestStatusService(context);

        var result = await service.UpdateAsync(
            -album.Id,
            new UpdatePrintRequestStatusDto { Status = status });

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        album.UserGalleryStatus.Should().Be(expected);
        album.IsSeenByAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task SeenService_MarkAllTouchesOnlyEligibleUnseenTargets()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var directUnseen = new PrintRequest
        {
            FullName = "Unseen",
            Email = "unseen@example.com",
            Status = "New",
            IsSeenByAdmin = false
        };
        var directSeen = new PrintRequest
        {
            FullName = "Seen",
            Email = "seen@example.com",
            Status = "New",
            IsSeenByAdmin = true
        };
        var eligible = UploadedAlbum("eligible", seen: false);
        var deleted = UploadedAlbum("deleted", seen: false);
        deleted.IsDeleted = true;
        var ordinary = new PortfolioAlbum
        {
            Title = "Ordinary",
            Slug = "ordinary",
            IsSeenByAdmin = false
        };

        context.AddRange(
            directUnseen,
            directSeen,
            eligible,
            deleted,
            ordinary);
        await context.SaveChangesAsync();

        var service = new AdminPrintRequestSeenService(context);

        var result = await service.MarkAllSeenAsync();

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        directUnseen.IsSeenByAdmin.Should().BeTrue();
        directUnseen.UpdatedAtUtc.Should().NotBeNull();
        directSeen.IsSeenByAdmin.Should().BeTrue();
        eligible.IsSeenByAdmin.Should().BeTrue();
        deleted.IsSeenByAdmin.Should().BeFalse();
        ordinary.IsSeenByAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task DeletionService_PreservesUploadedAlbumSoftDeletePolicy()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = UploadedAlbum("delete");
        album.IsPublished = true;
        album.AllowClientAccess = true;
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/uploads/photo.jpg",
            IsPublished = true,
            IsCover = true
        });
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();

        var service =
            new AdminPrintRequestDeletionService(context);

        var result = await service.DeleteAsync(-album.Id);

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        album.IsDeleted.Should().BeTrue();
        album.DeletedAtUtc.Should().NotBeNull();
        album.IsPublished.Should().BeFalse();
        album.AllowClientAccess.Should().BeFalse();
        album.IsSeenByAdmin.Should().BeTrue();
        album.UserGalleryStatus.Should()
            .Be(UserClientGalleryStatus.Expired);

        var image = album.Images.Single();
        image.IsDeleted.Should().BeTrue();
        image.DeletedAtUtc.Should().Be(album.DeletedAtUtc);
        image.IsPublished.Should().BeFalse();
        image.IsCover.Should().BeFalse();
    }

    [Fact]
    public async Task FacadeCompatibilityConstructor_PreservesDirectRequestFlow()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = new PrintRequest
        {
            FullName = "Client",
            Email = "client@example.com",
            Status = "New",
            IsSeenByAdmin = false
        };
        context.PrintRequests.Add(request);
        await context.SaveChangesAsync();

        var service =
            new AdminPrintRequestCommandService(context);

        var updated = await service.UpdateStatusAsync(
            request.Id,
            new UpdatePrintRequestStatusDto
            {
                Status = "Completed"
            });
        var seen = await service.MarkSeenAsync(request.Id);

        updated.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        seen.StatusCode.Should().Be(StatusCodes.Status204NoContent);

        var stored = await context.PrintRequests.SingleAsync();
        stored.Status.Should().Be("Completed");
        stored.IsSeenByAdmin.Should().BeTrue();
        stored.UpdatedAtUtc.Should().NotBeNull();
    }

    private static PortfolioAlbum UploadedAlbum(
        string slug,
        bool seen = false) =>
        new()
        {
            Title = slug,
            Slug = slug,
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            IsSeenByAdmin = seen,
            UserGalleryStatus = UserClientGalleryStatus.Pending
        };
}
