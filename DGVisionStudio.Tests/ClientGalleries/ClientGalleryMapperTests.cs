using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using FluentAssertions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryMapperTests
{
    private readonly ClientGalleryMapper _mapper = new();

    [Fact]
    public void MapPhotoDto_UsesThumbnailAndDownloadRoute_ForImages()
    {
        var image = new PortfolioImage
        {
            Id = 7,
            ImageUrl = "/uploads/original.jpg",
            ThumbnailUrl = "/uploads/thumb.jpg",
            IsPublished = true
        };

        var result = _mapper.MapPhotoDto(image, canDownload: true, galleryId: 12);

        result.MediaType.Should().Be("Image");
        result.PreviewUrl.Should().Be("/uploads/thumb.jpg");
        result.OriginalUrl.Should().BeNull();
        result.DownloadUrl.Should().Be("/api/client-galleries/12/photos/7/download");
        result.CanDownload.Should().BeTrue();
    }

    [Fact]
    public void MapPhotoDto_DetectsVideoAndContentTypeWithQueryString()
    {
        var image = new PortfolioImage
        {
            Id = 8,
            ImageUrl = "https://cdn.example.com/video.MP4?v=2"
        };

        var result = _mapper.MapPhotoDto(image, canDownload: false, galleryId: 12);

        result.MediaType.Should().Be("Video");
        result.ContentType.Should().Be("video/mp4");
        result.PreviewUrl.Should().Be(image.ImageUrl);
        result.OriginalUrl.Should().Be(image.ImageUrl);
        result.DownloadUrl.Should().BeNull();
        result.CanDownload.Should().BeFalse();
    }

    [Fact]
    public void MapGalleryDto_MarksExpiredUserUploadAndDisablesOwnerDownload()
    {
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        var album = new PortfolioAlbum
        {
            Id = 1,
            Title = "Expired upload",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            ExpiresAtUtc = now.AddMinutes(-1),
            UserGalleryStatus = UserClientGalleryStatus.Pending
        };

        var result = _mapper.MapGalleryDto(album, now, access: null, isOwner: true);

        result.IsExpired.Should().BeFalse();
        result.DownloadEnabled.Should().BeFalse();
        result.RemainingLifetimeDays.Should().Be(0);
        result.UserGalleryStatus.Should().Be(UserClientGalleryStatus.Expired);
    }

    [Fact]
    public void MapGalleryDetailsDto_FiltersUnpublishedPhotosForClientAndKeepsOrder()
    {
        var album = new PortfolioAlbum
        {
            Id = 4,
            Title = "Gallery",
            Images =
            {
                new PortfolioImage { Id = 3, ImageUrl = "/three.jpg", DisplayOrder = 2, IsPublished = true },
                new PortfolioImage { Id = 2, ImageUrl = "/hidden.jpg", DisplayOrder = 0, IsPublished = false },
                new PortfolioImage { Id = 1, ImageUrl = "/one.jpg", DisplayOrder = 1, IsPublished = true },
                new PortfolioImage { Id = 5, ImageUrl = "/deleted.jpg", DisplayOrder = 0, IsPublished = true, IsDeleted = true }
            }
        };

        var result = _mapper.MapGalleryDetailsDto(
            album,
            DateTime.UtcNow,
            access: null,
            canDownload: false);

        result.Photos.Select(x => x.Id).Should().Equal(1, 3);
    }

    [Fact]
    public void IsDownloadActive_RespectsEnabledFlagAndExpiration()
    {
        var now = DateTime.UtcNow;

        _mapper.IsDownloadActive(new UserAlbumAccess
        {
            DownloadEnabled = true,
            DownloadExpiresAtUtc = now.AddMinutes(1)
        }, now).Should().BeTrue();

        _mapper.IsDownloadActive(new UserAlbumAccess
        {
            DownloadEnabled = true,
            DownloadExpiresAtUtc = now.AddMinutes(-1)
        }, now).Should().BeFalse();

        _mapper.IsDownloadActive(new UserAlbumAccess
        {
            DownloadEnabled = false
        }, now).Should().BeFalse();
    }
}
