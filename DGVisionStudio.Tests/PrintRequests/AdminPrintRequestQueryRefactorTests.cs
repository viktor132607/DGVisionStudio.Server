using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;

namespace DGVisionStudio.Tests.PrintRequests;

public sealed class AdminPrintRequestQueryRefactorTests
{
    [Theory]
    [InlineData(UserClientGalleryStatus.PrintInProgress, "InProgress")]
    [InlineData(UserClientGalleryStatus.Processed, "Completed")]
    [InlineData(UserClientGalleryStatus.Expired, "Cancelled")]
    [InlineData(UserClientGalleryStatus.Pending, "New")]
    public void Mapper_MapsClientUploadStatuses(
        UserClientGalleryStatus status,
        string expected)
    {
        var mapper = new AdminPrintRequestMapper();

        mapper.MapClientPrintUploadStatus(status)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void Mapper_FiltersDeletedUploadedImagesAndOrdersRemaining()
    {
        var album = new PortfolioAlbum
        {
            Id = 9,
            Title = "Upload",
            Slug = "upload",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            Images =
            {
                new PortfolioImage
                {
                    Id = 3,
                    ImageUrl = "/third.jpg",
                    DisplayOrder = 3
                },
                new PortfolioImage
                {
                    Id = 1,
                    ImageUrl = "/first.jpg",
                    DisplayOrder = 1
                },
                new PortfolioImage
                {
                    Id = 2,
                    ImageUrl = "/deleted.jpg",
                    DisplayOrder = 0,
                    IsDeleted = true
                }
            }
        };

        var dto = new AdminPrintRequestMapper()
            .ToUserUploadedAlbumDto(album);

        dto.Id.Should().Be(-9);
        dto.Items
            .Select(x => x.ImageUrl)
            .Should()
            .Equal("/first.jpg", "/third.jpg");
    }

    [Fact]
    public async Task DirectQueryService_LoadsNestedImageData()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var album = new PortfolioAlbum
        {
            Title = "Album",
            Slug = "album"
        };
        var image = new PortfolioImage
        {
            ImageUrl = "/photo.jpg"
        };
        album.Images.Add(image);

        var request = new PrintRequest
        {
            PortfolioAlbum = album,
            FullName = "Client",
            Email = "client@example.com",
            Items =
            {
                new PrintRequestItem
                {
                    PortfolioImage = image,
                    Quantity = 2,
                    Size = "10x15"
                }
            }
        };

        context.PrintRequests.Add(request);
        await context.SaveChangesAsync();

        var service = new AdminDirectPrintRequestQueryService(
            context,
            new AdminPrintRequestMapper());

        var dto = await service.GetByIdAsync(request.Id);

        dto.Should().NotBeNull();
        dto!.AlbumTitle.Should().Be("Album");
        dto.Items.Should().ContainSingle();
        dto.Items[0].ImageUrl.Should().Be("/photo.jpg");
        dto.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task UploadedQueryService_RejectsDeletedAndNonUploadAlbums()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var normal = new PortfolioAlbum
        {
            Title = "Normal",
            Slug = "normal",
            GalleryType = GalleryType.Photoshoot,
            IsUserUploaded = true
        };
        var deleted = new PortfolioAlbum
        {
            Title = "Deleted",
            Slug = "deleted",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        context.PortfolioAlbums.AddRange(normal, deleted);
        await context.SaveChangesAsync();

        var service = new AdminUploadedPrintRequestQueryService(
            context,
            new AdminPrintRequestMapper());

        (await service.GetAllAsync()).Should().BeEmpty();
        (await service.GetByAlbumIdAsync(normal.Id)).Should().BeNull();
        (await service.GetByAlbumIdAsync(deleted.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Facade_MergesSourcesByCreatedDateAndRoutesNegativeIds()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var directAlbum = new PortfolioAlbum
        {
            Title = "Direct",
            Slug = "direct"
        };
        var direct = new PrintRequest
        {
            PortfolioAlbum = directAlbum,
            FullName = "Client",
            Email = "client@example.com",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var uploaded = new PortfolioAlbum
        {
            Title = "Uploaded",
            Slug = "uploaded",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.AddRange(direct, uploaded);
        await context.SaveChangesAsync();

        var mapper = new AdminPrintRequestMapper();
        var facade = new AdminPrintRequestQueryService(
            new AdminDirectPrintRequestQueryService(
                context,
                mapper),
            new AdminUploadedPrintRequestQueryService(
                context,
                mapper));

        var all = await facade.GetAllAsync();
        var byId = await facade.GetByIdAsync(-uploaded.Id);

        var items = all.Value
            .Should()
            .BeAssignableTo<List<DGVisionStudio.Application.DTOs.PrintRequests.PrintRequestDto>>()
            .Subject;

        items.Should().HaveCount(2);
        items[0].Id.Should().Be(-uploaded.Id);
        byId.Value
            .Should()
            .BeOfType<DGVisionStudio.Application.DTOs.PrintRequests.PrintRequestDto>()
            .Which.Id.Should().Be(-uploaded.Id);
    }
}
