using System.IO.Compression;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryEndpointRefactorTests
{
    [Fact]
    public async Task UserContext_ReturnsUserOrNullFromUserManager()
    {
        var user = TestUsers.Create(
            "user@example.com",
            "user-1");

        var resolved = await new ClientGalleryEndpointUserContextService(
                new TestUserManager(user))
            .ResolveAsync(TestUsers.CreatePrincipal(user));

        var missing = await new ClientGalleryEndpointUserContextService(
                new TestUserManager(null))
            .ResolveAsync(new System.Security.Claims.ClaimsPrincipal());

        resolved.Should().BeSameAs(user);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task UserEndpoint_ValidatesCreateAndDelegatesSuccessfulCreate()
    {
        var user = TestUsers.Create(id: "user-1");
        var galleryService = new StubClientGalleryService
        {
            CreateUserGallery = (userId, _) =>
            {
                userId.Should().Be(user.Id);
                return Task.FromResult<int?>(42);
            }
        };
        var endpoint = new ClientGalleryUserEndpointService(
            galleryService,
            new ClientGalleryEndpointUserContextService(
                new TestUserManager(user)));
        var principal = TestUsers.CreatePrincipal(user);

        var invalid = await endpoint.CreateMyGalleryAsync(
            principal,
            new CreateUserClientGalleryRequest { Title = " " });

        var created = await endpoint.CreateMyGalleryAsync(
            principal,
            new CreateUserClientGalleryRequest
            {
                Title = "Gallery"
            });

        invalid.StatusCode.Should().Be(
            StatusCodes.Status400BadRequest);
        created.StatusCode.Should().Be(
            StatusCodes.Status200OK);
    }

    [Fact]
    public async Task PhotoDownload_PassesAdminRoleAndWrapsFileResult()
    {
        var user = TestUsers.Create(id: "admin-1");
        bool? capturedAdmin = null;

        var galleryService = new StubClientGalleryService
        {
            OpenDownload = (_, _, userId, isAdmin) =>
            {
                userId.Should().Be(user.Id);
                capturedAdmin = isAdmin;
                return Task.FromResult<
                    (Stream, string, string)?>(
                    (new MemoryStream([1, 2, 3]),
                    "image/jpeg",
                    "photo.jpg"));
            }
        };

        var service =
            new ClientGalleryPhotoDownloadEndpointService(
                galleryService,
                new ClientGalleryEndpointUserContextService(
                    new TestUserManager(user)));

        var result = await service.DownloadPhotoAsync(
            TestUsers.CreatePrincipal(user, isAdmin: true),
            7,
            9);

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<FileDownloadResult>();
        capturedAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task ZipDownload_BuildsOrderedArchiveFromPublishedImages()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var album = new PortfolioAlbum
        {
            Title = "Client Gallery",
            Slug = "client-gallery",
            AllowClientAccess = true
        };

        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/two.jpg",
            DisplayOrder = 2,
            IsPublished = true
        });
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/one.jpg",
            DisplayOrder = 1,
            IsPublished = true
        });
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/hidden.jpg",
            DisplayOrder = 0,
            IsPublished = false
        });

        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();

        var storage = new StubFileStorageService();
        storage.Files["/one.jpg"] = [1];
        storage.Files["/two.jpg"] = [2];

        var user = TestUsers.Create(id: "user-1");
        var galleryService = new StubClientGalleryService
        {
            UserCanAccess = (_, userId, requireDownload) =>
            {
                userId.Should().Be(user.Id);
                requireDownload.Should().BeTrue();
                return Task.FromResult(true);
            }
        };

        var service = new ClientGalleryZipDownloadService(
            galleryService,
            new ClientGalleryEndpointUserContextService(
                new TestUserManager(user)),
            context,
            storage);

        var result = await service.DownloadGalleryZipAsync(
            TestUsers.CreatePrincipal(user),
            album.Id);

        result.StatusCode.Should().Be(StatusCodes.Status200OK);

        var file = result.Value
            .Should()
            .BeOfType<FileDownloadResult>()
            .Subject;

        file.FileName.Should().Be("Client Gallery.zip");

        using var archive = new ZipArchive(
            file.Stream,
            ZipArchiveMode.Read);

        archive.Entries.Select(x => x.Name).Should().Equal(
            $"001-{album.Images.Single(x => x.ImageUrl == "/one.jpg").Id}.jpg",
            $"002-{album.Images.Single(x => x.ImageUrl == "/two.jpg").Id}.jpg");
    }

    [Fact]
    public async Task ZipDownload_ReturnsForbiddenWithoutDownloadPermission()
    {
        await using var context =
            TestDbContextFactory.CreateContext();

        var user = TestUsers.Create(id: "user-1");
        var service = new ClientGalleryZipDownloadService(
            new StubClientGalleryService
            {
                UserCanAccess = (_, _, _) =>
                    Task.FromResult(false)
            },
            new ClientGalleryEndpointUserContextService(
                new TestUserManager(user)),
            context,
            new StubFileStorageService());

        var result = await service.DownloadGalleryZipAsync(
            TestUsers.CreatePrincipal(user),
            10);

        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void ZipDownload_SafeArchiveNameFallsBackWhenTitleIsInvalid()
    {
        ClientGalleryZipDownloadService
            .BuildSafeArchiveName("/", 17)
            .Should()
            .Be("gallery-17");
    }
}
