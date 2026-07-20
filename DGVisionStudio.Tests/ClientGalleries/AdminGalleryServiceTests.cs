using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class AdminGalleryMediaDownloadServiceTests
{
    [Fact]
    public async Task DownloadPhotoAsync_ReturnsBadRequest_ForInvalidIds()
    {
        var service = new AdminGalleryMediaDownloadService(
            new StubClientGalleryService(),
            new RecordingAuditLogService(),
            NullLogger<AdminGalleryMediaDownloadService>.Instance);

        var result = await service.DownloadPhotoAsync(0, 1, Context());

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DownloadPhotoAsync_ReturnsFileAndWritesAudit_WhenPhotoExists()
    {
        var gallery = new StubClientGalleryService
        {
            OpenDownload = (_, _, _, _) => Task.FromResult<(Stream, string, string)?>(
                (new MemoryStream([1, 2, 3]), "image/jpeg", "photo.jpg"))
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryMediaDownloadService(
            gallery,
            audit,
            NullLogger<AdminGalleryMediaDownloadService>.Instance);

        var result = await service.DownloadPhotoAsync(5, 7, Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<FileDownloadResult>();
        audit.Entries.Should().ContainSingle(x => x.Action == "DownloadPhoto" && x.EntityId == "7");
    }

    private static AdminRequestContext Context() => new("admin", "admin@test.bg", "Admin", null, "tests", "trace");
}

public sealed class AdminGalleryMediaUploadServiceTests
{
    [Fact]
    public async Task UploadPhotoAsync_ReturnsBadRequest_ForInvalidGallery()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var service = Create(db);

        var result = await service.UploadPhotoAsync(
            0,
            GalleryTestFiles.Create("photo.png", "image/png"),
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UploadVideoAsync_RejectsNonVideoFiles()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var service = Create(db);

        var result = await service.UploadVideoAsync(
            1,
            GalleryTestFiles.Create("photo.png", "image/png"),
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AdminGalleryMediaUploadService Create(DGVisionStudio.Infrastructure.Data.AppDbContext db) =>
        new(
            new StubClientGalleryService(),
            new RecordingAuditLogService(),
            NullLogger<AdminGalleryMediaUploadService>.Instance,
            db,
            new TestWebHostEnvironment { ContentRootPath = Path.GetTempPath() },
            new ClientGalleryMapper());

    private static AdminRequestContext Context() => new("admin", "admin@test.bg", "Admin", null, "tests", "trace");
}

public sealed class AdminGalleryMediaMutationServiceTests
{
    [Fact]
    public async Task MutationMethods_ValidateTheirInputBeforeDelegating()
    {
        var service = new AdminGalleryMediaMutationService(
            new StubClientGalleryService(),
            new RecordingAuditLogService(),
            NullLogger<AdminGalleryMediaMutationService>.Instance);
        var context = new AdminRequestContext("admin", "admin@test.bg", "Admin", null, "tests", "trace");

        (await service.UpdatePhotoAsync(0, 1, new UpdateClientPhotoRequest(), context)).StatusCode
            .Should().Be(StatusCodes.Status400BadRequest);
        (await service.DeletePhotoAsync(1, 0, context)).StatusCode
            .Should().Be(StatusCodes.Status400BadRequest);
        (await service.SetCoverImageAsync(1, new SetGalleryCoverRequest(), context)).StatusCode
            .Should().Be(StatusCodes.Status400BadRequest);
        (await service.ReorderPhotosAsync(1, new ReorderGalleryPhotosRequest(), context)).StatusCode
            .Should().Be(StatusCodes.Status400BadRequest);
    }
}

public sealed class AdminGalleryMediaManagementServiceTests
{
    [Fact]
    public async Task UpdateMetadataAsync_DelegatesToMetadataService()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var fakeGallery = new StubClientGalleryService();
        var audit = new RecordingAuditLogService();
        var mapper = new ClientGalleryMapper();
        var metadata = new AdminGalleryMediaMetadataService(db);
        var downloads = new AdminGalleryMediaDownloadService(fakeGallery, audit, NullLogger<AdminGalleryMediaDownloadService>.Instance);
        var uploads = new AdminGalleryMediaUploadService(
            fakeGallery,
            audit,
            NullLogger<AdminGalleryMediaUploadService>.Instance,
            db,
            new TestWebHostEnvironment { ContentRootPath = Path.GetTempPath() },
            mapper);
        var mutations = new AdminGalleryMediaMutationService(fakeGallery, audit, NullLogger<AdminGalleryMediaMutationService>.Instance);
        var service = new AdminGalleryMediaManagementService(metadata, downloads, uploads, mutations);

        var result = await service.UpdateMetadataAsync(0, 1, new UpdateGalleryMediaMetadataRequest());

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

public sealed class AdminGalleryArchiveServiceTests
{
    [Fact]
    public async Task ArchiveMethods_ReturnNotFound_WhenThereAreNoAlbums()
    {
        await using var db = TestDbContextFactory.CreateContext();
        var service = new AdminGalleryArchiveService(
            db,
            new StubFileStorageService(),
            NullLogger<AdminGalleryArchiveService>.Instance);

        var physical = await service.CreatePhysicalArchiveAsync(CancellationToken.None);
        var streaming = await service.PrepareStreamingArchiveAsync(CancellationToken.None);

        physical.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        streaming.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public sealed class AdminGalleryAccessEndpointServiceTests
{
    [Fact]
    public async Task GrantAccessAsync_WritesAudit_WhenGrantSucceeds()
    {
        var gallery = new StubClientGalleryService
        {
            GrantAccess = (_, _) => Task.FromResult(true)
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryAccessEndpointService(
            gallery,
            audit,
            NullLogger<AdminGalleryAccessEndpointService>.Instance);
        var request = new GrantGalleryAccessRequest
        {
            UserEmail = "user@test.bg",
            PreviewEnabled = true,
            DownloadEnabled = true
        };

        var result = await service.GrantAccessAsync(4, request, Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(x => x.Action == "GrantGalleryAccess" && x.EntityId == "4");
    }

    [Fact]
    public async Task GetGalleryAccessesAsync_ReturnsBadRequest_ForInvalidGallery()
    {
        var service = new AdminGalleryAccessEndpointService(
            new StubClientGalleryService(),
            new RecordingAuditLogService(),
            NullLogger<AdminGalleryAccessEndpointService>.Instance);

        var result = await service.GetGalleryAccessesAsync(0);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AdminRequestContext Context() => new("admin", "admin@test.bg", "Admin", null, "tests", "trace");
}
