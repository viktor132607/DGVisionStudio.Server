using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Tests.PrintRequests;

public sealed class AdminPrintRequestQueryServiceTests
{
    [Fact]
    public async Task GetAllAsync_CombinesDirectRequestsAndUploadedAlbums()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("client@example.com");
        var directAlbum = new PortfolioAlbum { Title = "Direct album", Slug = "direct-album" };
        var directImage = new PortfolioImage { ImageUrl = "/direct.jpg", DisplayOrder = 1 };
        directAlbum.Images.Add(directImage);
        var directRequest = new PrintRequest
        {
            User = user,
            UserId = user.Id,
            PortfolioAlbum = directAlbum,
            FullName = "Direct client",
            Email = user.Email!,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Items =
            {
                new PrintRequestItem
                {
                    PortfolioImage = directImage,
                    Quantity = 2,
                    Size = "10x15"
                }
            }
        };
        var uploadedAlbum = new PortfolioAlbum
        {
            Title = "Uploaded album",
            Slug = "uploaded-album",
            OwnerUser = user,
            OwnerUserId = user.Id,
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            UserGalleryStatus = UserClientGalleryStatus.PrintInProgress,
            CreatedAtUtc = DateTime.UtcNow,
            Images =
            {
                new PortfolioImage { ImageUrl = "/second.jpg", DisplayOrder = 2 },
                new PortfolioImage { ImageUrl = "/first.jpg", DisplayOrder = 1 },
                new PortfolioImage { ImageUrl = "/deleted.jpg", DisplayOrder = 0, IsDeleted = true }
            }
        };
        context.PrintRequests.Add(directRequest);
        context.PortfolioAlbums.Add(uploadedAlbum);
        await context.SaveChangesAsync();
        var service = new AdminPrintRequestQueryService(context);

        var result = await service.GetAllAsync();

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var items = result.Value.Should().BeAssignableTo<List<PrintRequestDto>>().Subject;
        items.Should().HaveCount(2);
        items[0].Id.Should().Be(-uploadedAlbum.Id);
        items[0].Status.Should().Be("InProgress");
        items[0].Items.Select(x => x.ImageUrl).Should().Equal("/first.jpg", "/second.jpg");
        items[1].Id.Should().Be(directRequest.Id);
        items[1].Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUploadedAlbumUsingNegativeId()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var album = new PortfolioAlbum
        {
            Title = "Uploaded album",
            Slug = "uploaded-by-id",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            UserGalleryStatus = UserClientGalleryStatus.Processed
        };
        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        var service = new AdminPrintRequestQueryService(context);

        var result = await service.GetByIdAsync(-album.Id);

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var dto = result.Value.Should().BeOfType<PrintRequestDto>().Subject;
        dto.Id.Should().Be(-album.Id);
        dto.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenRequestDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminPrintRequestQueryService(context);

        var result = await service.GetByIdAsync(999);

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
