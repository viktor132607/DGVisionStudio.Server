using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class AdminGalleryMediaMetadataServiceTests
{
    [Fact]
    public async Task UpdateMetadataAsync_ReturnsBadRequest_ForInvalidIds()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminGalleryMediaMetadataService(context);

        var result = await service.UpdateMetadataAsync(
            0,
            1,
            new UpdateGalleryMediaMetadataRequest());

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateMetadataAsync_ReturnsNotFound_WhenMediaDoesNotBelongToGallery()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum { Title = "Gallery", Slug = "gallery" };
        var image = new PortfolioImage { ImageUrl = "/image.jpg" };
        album.Images.Add(image);
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new AdminGalleryMediaMetadataService(context);

        var result = await service.UpdateMetadataAsync(
            album.Id + 1,
            image.Id,
            new UpdateGalleryMediaMetadataRequest { Name = "Updated" });

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateMetadataAsync_TrimsNameAndClearsAltAndCaption()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum { Title = "Gallery", Slug = "metadata-gallery" };
        var image = new PortfolioImage
        {
            ImageUrl = "/image.jpg",
            Name = "Old name",
            AltText = "Old alt",
            Caption = "Old caption"
        };
        album.Images.Add(image);
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new AdminGalleryMediaMetadataService(context);

        var result = await service.UpdateMetadataAsync(
            album.Id,
            image.Id,
            new UpdateGalleryMediaMetadataRequest
            {
                Name = "  New name  ",
                ClearAltAndCaption = true
            });

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await context.PortfolioImages.SingleAsync();
        stored.Name.Should().Be("New name");
        stored.AltText.Should().BeNull();
        stored.Caption.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMetadataAsync_TruncatesNameToMaximumLength()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum { Title = "Gallery", Slug = "long-name-gallery" };
        var image = new PortfolioImage { ImageUrl = "/image.jpg" };
        album.Images.Add(image);
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new AdminGalleryMediaMetadataService(context);

        var result = await service.UpdateMetadataAsync(
            album.Id,
            image.Id,
            new UpdateGalleryMediaMetadataRequest { Name = new string('x', 300) });

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        (await context.PortfolioImages.SingleAsync()).Name.Should().HaveLength(250);
    }
}
