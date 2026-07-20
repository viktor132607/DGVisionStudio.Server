using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.PrintRequests;

public sealed class AdminPrintRequestCommandServiceTests
{
    [Fact]
    public async Task UpdateStatusAsync_ReturnsBadRequest_ForUnsupportedStatus()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminPrintRequestCommandService(context);

        var result = await service.UpdateStatusAsync(1, new UpdatePrintRequestStatusDto { Status = "Unknown" });

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesDirectPrintRequestAndMarksItSeen()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = new PrintRequest
        {
            FullName = "Client",
            Email = "client@example.com",
            Status = "New"
        };
        context.PrintRequests.Add(request);
        await context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(context);

        var result = await service.UpdateStatusAsync(
            request.Id,
            new UpdatePrintRequestStatusDto { Status = "Completed" });

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        var stored = await context.PrintRequests.SingleAsync();
        stored.Status.Should().Be("Completed");
        stored.IsSeenByAdmin.Should().BeTrue();
        stored.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_MapsNegativeAlbumIdToPrintGalleryStatus()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum
        {
            Title = "Uploaded print album",
            Slug = "uploaded-print-album",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            UserGalleryStatus = UserClientGalleryStatus.Pending
        };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(context);

        var result = await service.UpdateStatusAsync(
            -album.Id,
            new UpdatePrintRequestStatusDto { Status = "InProgress" });

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        var stored = await context.PortfolioAlbums.SingleAsync();
        stored.UserGalleryStatus.Should().Be(UserClientGalleryStatus.PrintInProgress);
        stored.IsSeenByAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesUploadedAlbumAndItsImages()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum
        {
            Title = "Uploaded print album",
            Slug = "uploaded-print-delete",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            IsPublished = true,
            AllowClientAccess = true,
            Images =
            {
                new PortfolioImage
                {
                    ImageUrl = "/uploads/one.jpg",
                    IsPublished = true,
                    IsCover = true
                }
            }
        };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new AdminPrintRequestCommandService(context);

        var result = await service.DeleteAsync(-album.Id);

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        var storedAlbum = await context.PortfolioAlbums.IgnoreQueryFilters().SingleAsync();
        storedAlbum.IsDeleted.Should().BeTrue();
        storedAlbum.IsPublished.Should().BeFalse();
        storedAlbum.AllowClientAccess.Should().BeFalse();
        storedAlbum.UserGalleryStatus.Should().Be(UserClientGalleryStatus.Expired);

        var storedImage = await context.PortfolioImages.IgnoreQueryFilters().SingleAsync();
        storedImage.IsDeleted.Should().BeTrue();
        storedImage.IsPublished.Should().BeFalse();
        storedImage.IsCover.Should().BeFalse();
    }
}
