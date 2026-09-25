using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using FluentAssertions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryMapperRefactorTests
{
    [Fact]
    public void AccessPolicy_PreservesExpiryBoundariesAndRemainingDays()
    {
        var policy = new ClientGalleryAccessPolicy();
        var now = new DateTime(
            2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        var access = new UserAlbumAccess
        {
            DownloadEnabled = true,
            DownloadExpiresAtUtc = now
        };

        policy.IsDownloadActive(access, now).Should().BeTrue();
        policy.IsExpired(access, now).Should().BeFalse();
        policy.GetRemainingDownloadDays(access, now)
            .Should().Be(0);

        access.DownloadExpiresAtUtc = now.AddTicks(-1);

        policy.IsDownloadActive(access, now).Should().BeFalse();
        policy.IsExpired(access, now).Should().BeTrue();
    }

    [Theory]
    [InlineData("/video.MP4?v=2", "video/mp4")]
    [InlineData("/video.mov#preview", "video/quicktime")]
    [InlineData("/video.webm", "video/webm")]
    [InlineData("/video.m4v", "video/x-m4v")]
    public void PhotoMapper_PreservesVideoDetectionAndContentTypes(
        string path,
        string contentType)
    {
        var mapper = new ClientGalleryPhotoMapper();
        var image = new PortfolioImage
        {
            Id = 4,
            ImageUrl = path,
            ThumbnailUrl = "/thumb.jpg"
        };

        var result = mapper.Map(
            image,
            canDownload: true,
            galleryId: 9);

        result.MediaType.Should().Be("Video");
        result.ContentType.Should().Be(contentType);
        result.PreviewUrl.Should().Be(path);
        result.OriginalUrl.Should().Be(path);
        result.DownloadUrl.Should()
            .Be("/api/client-galleries/9/photos/4/download");
    }

    [Fact]
    public void SummaryMapper_PreservesOwnerAdminAndAccessPermissions()
    {
        var now = DateTime.UtcNow;
        var policy = new ClientGalleryAccessPolicy();
        var mapper = new ClientGallerySummaryMapper(policy);
        var album = new PortfolioAlbum
        {
            Id = 5,
            Title = "Upload",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            ExpiresAtUtc = now.AddMinutes(-1),
            PortfolioCategory = new PortfolioCategory
            {
                Key = "client-galleries",
                Name = "Client",
                NameEn = "Client"
            }
        };
        var access = new UserAlbumAccess
        {
            PreviewEnabled = false,
            DownloadEnabled = true,
            DownloadExpiresAtUtc = now.AddDays(2)
        };

        var owner = mapper.Map(
            album,
            now,
            access: null,
            isOwner: true);
        var admin = mapper.Map(
            album,
            now,
            access,
            isAdminView: true);

        owner.DownloadEnabled.Should().BeFalse();
        owner.PreviewEnabled.Should().BeTrue();
        owner.UserGalleryStatus.Should()
            .Be(UserClientGalleryStatus.Expired);

        admin.DownloadEnabled.Should().BeTrue();
        admin.PreviewEnabled.Should().BeTrue();
        admin.IsPublic.Should().BeFalse();
    }

    [Fact]
    public void DetailsMapper_PreservesUserOrderingAndAdminPhotoVisibility()
    {
        var album = new PortfolioAlbum
        {
            Id = 7,
            Title = "Gallery"
        };

        album.UserAccesses.Add(new UserAlbumAccess
        {
            UserId = "b",
            User = new ApplicationUser
            {
                Id = "b",
                Email = "b@example.com"
            }
        });
        album.UserAccesses.Add(new UserAlbumAccess
        {
            UserId = "a",
            User = new ApplicationUser
            {
                Id = "a",
                Email = "a@example.com"
            }
        });
        album.Images.Add(new PortfolioImage
        {
            Id = 2,
            ImageUrl = "/hidden.jpg",
            IsPublished = false,
            DisplayOrder = 1
        });
        album.Images.Add(new PortfolioImage
        {
            Id = 1,
            ImageUrl = "/visible.jpg",
            IsPublished = true,
            DisplayOrder = 2
        });
        album.Images.Add(new PortfolioImage
        {
            Id = 3,
            ImageUrl = "/deleted.jpg",
            IsPublished = true,
            IsDeleted = true,
            DisplayOrder = 0
        });

        var mapper = new ClientGalleryDetailsMapper(
            new ClientGalleryAccessPolicy(),
            new ClientGalleryPhotoMapper());

        var client = mapper.Map(
            album,
            DateTime.UtcNow,
            access: null,
            canDownload: false);

        var admin = mapper.Map(
            album,
            DateTime.UtcNow,
            access: null,
            canDownload: true,
            isAdminView: true);

        client.Photos.Select(photo => photo.Id)
            .Should().Equal(1);
        admin.Photos.Select(photo => photo.Id)
            .Should().Equal(2, 1);
        admin.UserAccesses.Select(access => access.Email)
            .Should().Equal(
                "a@example.com",
                "b@example.com");
    }

    [Fact]
    public void Facade_CompatibilityConstructor_PreservesPublicApi()
    {
        var mapper = new ClientGalleryMapper();
        var image = new PortfolioImage
        {
            Id = 11,
            ImageUrl = "/image.jpg",
            ThumbnailUrl = "/thumb.jpg"
        };

        var photo = mapper.MapPhotoDto(
            image,
            canDownload: true,
            galleryId: 3);

        photo.PreviewUrl.Should().Be("/thumb.jpg");
        mapper.IsDownloadActive(
            new UserAlbumAccess
            {
                DownloadEnabled = true
            },
            DateTime.UtcNow).Should().BeTrue();
    }
}
