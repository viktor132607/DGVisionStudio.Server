using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Controllers;

public sealed class AdminClientGalleryAccessControllerTests
{
    [Fact]
    public async Task GrantAccess_ForwardsRequestAndAdminContext()
    {
        int capturedGallery = 0;
        GrantGalleryAccessRequest? capturedRequest = null;
        AdminRequestContext? capturedContext = null;
        var service = new StubAdminGalleryAccessEndpointService
        {
            GrantHandler = (galleryId, request, context) =>
            {
                capturedGallery = galleryId;
                capturedRequest = request;
                capturedContext = context;
                return Task.FromResult(ControllerServiceResult.Ok());
            }
        };
        var controller = new AdminClientGalleryAccessController(service);
        ControllerTestContext.Attach(controller);
        var request = new GrantGalleryAccessRequest { UserEmail = "client@example.com" };

        var result = await controller.GrantAccess(9, request);

        result.Should().BeOfType<OkResult>();
        capturedGallery.Should().Be(9);
        capturedRequest.Should().BeSameAs(request);
        capturedContext!.UserId.Should().Be("admin-1");
        capturedContext.TraceId.Should().Be("controller-trace");
    }
}

public sealed class AdminClientGalleryPhotosControllerTests
{
    [Fact]
    public async Task DownloadPhoto_ReturnsFileStreamResult()
    {
        AdminRequestContext? capturedContext = null;
        var stream = new MemoryStream([1, 2, 3]);
        var service = new StubAdminGalleryMediaManagementService
        {
            DownloadHandler = (galleryId, photoId, context) =>
            {
                galleryId.Should().Be(4);
                photoId.Should().Be(7);
                capturedContext = context;
                return Task.FromResult(ControllerServiceResult.Ok(
                    new FileDownloadResult(stream, "image/jpeg", "photo.jpg")));
            }
        };
        var controller = new AdminClientGalleryPhotosController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.DownloadPhoto(4, 7);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.FileStream.Should().BeSameAs(stream);
        file.ContentType.Should().Be("image/jpeg");
        file.FileDownloadName.Should().Be("photo.jpg");
        capturedContext!.Email.Should().Be("admin@example.com");
    }

    [Fact]
    public async Task UploadVideo_ForwardsFileAndGallery()
    {
        IFormFile? capturedFile = null;
        int capturedGallery = 0;
        var service = new StubAdminGalleryMediaManagementService
        {
            UploadVideoHandler = (galleryId, file, _) =>
            {
                capturedGallery = galleryId;
                capturedFile = file;
                return Task.FromResult(ControllerServiceResult.Ok(new { Id = 3 }));
            }
        };
        var controller = new AdminClientGalleryPhotosController(service);
        ControllerTestContext.Attach(controller);
        var upload = GalleryTestFiles.Create("clip.mp4", "video/mp4", [1, 2, 3]);

        var result = await controller.UploadVideo(5, upload);

        result.Should().BeOfType<OkObjectResult>();
        capturedGallery.Should().Be(5);
        capturedFile.Should().BeSameAs(upload);
    }
}

public sealed class AdminClientGalleryMediaControllerTests
{
    [Fact]
    public async Task UpdateMetadata_ForwardsRouteValuesAndBody()
    {
        int capturedGallery = 0;
        int capturedMedia = 0;
        UpdateGalleryMediaMetadataRequest? capturedRequest = null;
        var service = new StubAdminGalleryMediaManagementService
        {
            MetadataHandler = (galleryId, mediaId, request) =>
            {
                capturedGallery = galleryId;
                capturedMedia = mediaId;
                capturedRequest = request;
                return Task.FromResult(ControllerServiceResult.NoContent());
            }
        };
        var controller = new AdminClientGalleryMediaController(service);
        ControllerTestContext.Attach(controller);
        var request = new UpdateGalleryMediaMetadataRequest { Name = "Portrait", ClearAltAndCaption = true };

        var result = await controller.UpdateMetadata(2, 8, request);

        result.Should().BeOfType<NoContentResult>();
        capturedGallery.Should().Be(2);
        capturedMedia.Should().Be(8);
        capturedRequest.Should().BeSameAs(request);
    }
}

public sealed class AdminClientGalleriesArchiveControllerTests
{
    [Fact]
    public async Task DownloadAllAlbumsFile_ReturnsPhysicalFileResult()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var service = new StubAdminGalleryArchiveService
            {
                PhysicalHandler = _ => Task.FromResult(ControllerServiceResult.Ok(
                    new PhysicalFileDownloadResult(
                        tempPath,
                        "application/zip",
                        "albums.zip",
                        () => Task.CompletedTask)))
            };
            var controller = new AdminClientGalleriesArchiveController(service);
            ControllerTestContext.Attach(controller);

            var result = await controller.DownloadAllAlbumsFile(CancellationToken.None);

            var file = result.Should().BeOfType<PhysicalFileResult>().Subject;
            file.FileName.Should().Be(tempPath);
            file.ContentType.Should().Be("application/zip");
            file.FileDownloadName.Should().Be("albums.zip");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task DownloadAllAlbumsFile_MapsNoContentToEmptyResult()
    {
        var service = new StubAdminGalleryArchiveService
        {
            PhysicalHandler = _ => Task.FromResult(ControllerServiceResult.NoContent())
        };
        var controller = new AdminClientGalleriesArchiveController(service);
        ControllerTestContext.Attach(controller);

        var result = await controller.DownloadAllAlbumsFile(CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
    }
}

public sealed class AdminClientGalleriesDownloadControllerTests
{
    [Fact]
    public async Task DownloadAllAlbumsStream_WritesArchiveAndResponseHeaders()
    {
        var service = new StubAdminGalleryArchiveService
        {
            StreamingHandler = _ => Task.FromResult(ControllerServiceResult.Ok(
                new StreamingFileDownloadResult(
                    "application/zip",
                    "albums.zip",
                    async (destination, token) => await destination.WriteAsync(new byte[] { 4, 5, 6 }, token))))
        };
        var controller = new AdminClientGalleriesDownloadController(service);
        var context = ControllerTestContext.Attach(controller);

        var result = await controller.DownloadAllAlbumsStream(CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        context.Response.ContentType.Should().Be("application/zip");
        context.Response.Headers.ContentDisposition.ToString().Should().Contain("albums.zip");
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(4, 5, 6);
    }
}
