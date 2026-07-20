using System.IO.Compression;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Coverage;

public sealed class AdminGalleryArchiveCoverageTests
{
    [Fact]
    public async Task CreatePhysicalArchiveAsync_WritesPhotosAndCleansTemporaryFile()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        const string imageUrl = "/uploads/gallery/photo.jpg";
        await SeedAlbumAsync(fixture.Context, imageUrl);
        var storage = new StubFileStorageService();
        storage.Files[imageUrl] = [1, 2, 3, 4];
        var service = new AdminGalleryArchiveService(
            fixture.Context,
            storage,
            NullLogger<AdminGalleryArchiveService>.Instance);

        var result = await service.CreatePhysicalArchiveAsync(CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var download = result.Value.Should().BeOfType<PhysicalFileDownloadResult>().Subject;
        File.Exists(download.Path).Should().BeTrue();
        using (var archive = ZipFile.OpenRead(download.Path))
        {
            archive.Entries.Should().ContainSingle();
            archive.Entries[0].FullName.Should().EndWith(".jpg");
        }

        await download.CleanupAsync();
        File.Exists(download.Path).Should().BeFalse();
    }

    [Fact]
    public async Task PrepareStreamingArchiveAsync_WritesReadme_WhenEveryPhotoIsMissing()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        await SeedAlbumAsync(fixture.Context, "/uploads/gallery/missing.jpg");
        var service = new AdminGalleryArchiveService(
            fixture.Context,
            new StubFileStorageService(),
            NullLogger<AdminGalleryArchiveService>.Instance);

        var result = await service.PrepareStreamingArchiveAsync(CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var download = result.Value.Should().BeOfType<StreamingFileDownloadResult>().Subject;
        await using var destination = new MemoryStream();
        await download.WriteAsync(destination, CancellationToken.None);
        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true);
        archive.Entries.Should().ContainSingle(x => x.FullName == "README.txt");
    }

    [Fact]
    public async Task ArchiveMethods_ReturnNoPhotos_WhenAlbumContainsNoMedia()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        await SeedAlbumAsync(fixture.Context, imageUrl: null);
        var service = new AdminGalleryArchiveService(
            fixture.Context,
            new StubFileStorageService(),
            NullLogger<AdminGalleryArchiveService>.Instance);

        var physical = await service.CreatePhysicalArchiveAsync(CancellationToken.None);
        var streaming = await service.PrepareStreamingArchiveAsync(CancellationToken.None);

        physical.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        streaming.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static async Task SeedAlbumAsync(
        DGVisionStudio.Infrastructure.Data.AppDbContext context,
        string? imageUrl)
    {
        var category = new PortfolioCategory
        {
            Key = $"category-{Guid.NewGuid():N}",
            Name = "Category/Unsafe",
            NameEn = "Category",
            DisplayOrder = 1
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = $"album-{Guid.NewGuid():N}",
            Title = "Album: Unsafe",
            DisplayOrder = 2,
            IsPublished = true,
            IsDeleted = false
        };
        context.AddRange(category, album);
        if (imageUrl is not null)
        {
            context.PortfolioImages.Add(new PortfolioImage
            {
                PortfolioAlbum = album,
                ImageUrl = imageUrl,
                DisplayOrder = 3,
                IsPublished = true,
                IsDeleted = false
            });
        }
        await context.SaveChangesAsync();
    }
}

public sealed class AdminGalleryMediaMutationCoverageTests
{
    [Fact]
    public async Task UpdateAndDeletePhoto_HandleSuccessAndMissingPhoto()
    {
        var gallery = new StubClientGalleryService
        {
            UpdatePhoto = (_, photoId, _) => Task.FromResult<ClientPhotoDto?>(
                photoId == 7 ? new ClientPhotoDto { Id = photoId } : null),
            DeletePhoto = (_, photoId) => Task.FromResult(photoId == 7)
        };
        var audit = new RecordingAuditLogService();
        var service = Create(gallery, audit);

        var updated = await service.UpdatePhotoAsync(
            4,
            7,
            new UpdateClientPhotoRequest { IsPublished = true, IsCover = false },
            Context());
        var missing = await service.UpdatePhotoAsync(
            4,
            8,
            new UpdateClientPhotoRequest(),
            Context());
        var deleted = await service.DeletePhotoAsync(4, 7, Context());
        var deleteMissing = await service.DeletePhotoAsync(4, 8, Context());

        updated.StatusCode.Should().Be(StatusCodes.Status200OK);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        deleted.StatusCode.Should().Be(StatusCodes.Status200OK);
        deleteMissing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        audit.Entries.Should().Contain(x => x.Action == "UpdateGalleryPhoto" && x.EntityId == "7");
        audit.Entries.Should().Contain(x => x.Action == "DeleteGalleryPhoto" && x.EntityId == "7");
    }

    [Fact]
    public async Task CoverAndReorder_ValidateThenAuditSuccessfulChanges()
    {
        var gallery = new StubClientGalleryService
        {
            SetCover = (_, url) => Task.FromResult(url == "/cover.jpg"),
            Reorder = (_, ids) => Task.FromResult(ids.SequenceEqual([3, 2, 1]))
        };
        var audit = new RecordingAuditLogService();
        var service = Create(gallery, audit);

        var invalidCover = await service.SetCoverImageAsync(
            4,
            new SetGalleryCoverRequest { CoverImageUrl = " " },
            Context());
        var cover = await service.SetCoverImageAsync(
            4,
            new SetGalleryCoverRequest { CoverImageUrl = "/cover.jpg" },
            Context());
        var invalidOrder = await service.ReorderPhotosAsync(
            4,
            new ReorderGalleryPhotosRequest { OrderedPhotoIds = [] },
            Context());
        var reordered = await service.ReorderPhotosAsync(
            4,
            new ReorderGalleryPhotosRequest { OrderedPhotoIds = [3, 2, 1] },
            Context());

        invalidCover.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        cover.StatusCode.Should().Be(StatusCodes.Status200OK);
        invalidOrder.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        reordered.StatusCode.Should().Be(StatusCodes.Status200OK);
        audit.Entries.Should().Contain(x => x.Action == "SetGalleryCover");
        audit.Entries.Should().Contain(x => x.Action == "ReorderGalleryPhotos");
    }

    private static AdminGalleryMediaMutationService Create(
        StubClientGalleryService gallery,
        RecordingAuditLogService audit) =>
        new(gallery, audit, NullLogger<AdminGalleryMediaMutationService>.Instance);

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace");
}

public sealed class AdminGalleryMediaUploadCoverageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dg-gallery-upload-{Guid.NewGuid():N}");

    [Fact]
    public async Task UploadPhotoAsync_HandlesSuccessMissingGalleryAndInvalidContent()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var gallery = new StubClientGalleryService
        {
            UploadPhoto = (_, file) => Task.FromResult<ClientPhotoDto?>(
                file.FileName == "ok.jpg" ? new ClientPhotoDto { Id = 11 } : null)
        };
        var audit = new RecordingAuditLogService();
        var service = Create(context, gallery, audit);

        var invalid = await service.UploadPhotoAsync(
            1,
            GalleryTestFiles.Create("notes.txt", "text/plain"),
            Context());
        var missing = await service.UploadPhotoAsync(
            1,
            GalleryTestFiles.Create("missing.jpg", "image/jpeg"),
            Context());
        var uploaded = await service.UploadPhotoAsync(
            1,
            GalleryTestFiles.Create("ok.jpg", "image/jpeg"),
            Context());

        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        uploaded.StatusCode.Should().Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(x => x.Action == "UploadGalleryPhoto" && x.EntityId == "11");
    }

    [Fact]
    public async Task UploadVideoAsync_PersistsMediaFileAndAuditEntry()
    {
        Directory.CreateDirectory(_root);
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory { Key = "video", Name = "Видео", NameEn = "Video" };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "video-gallery",
            Title = "Video gallery",
            IsDeleted = false
        };
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, new StubClientGalleryService(), audit);

        var result = await service.UploadVideoAsync(
            album.Id,
            GalleryTestFiles.Create("intro.mp4", "video/mp4", [0, 0, 0, 24]),
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var media = await fixture.Context.PortfolioImages.SingleAsync();
        media.ImageUrl.Should().StartWith("/uploads/portfolio/videos/").And.EndWith(".mp4");
        media.DisplayOrder.Should().Be(1);
        File.Exists(Path.Combine(_root, "wwwroot", media.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeTrue();
        audit.Entries.Should().ContainSingle(x => x.Action == "UploadGalleryVideo" && x.EntityId == media.Id.ToString());
    }

    [Fact]
    public async Task UploadVideoAsync_ReturnsNotFound_WhenGalleryDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = Create(context, new StubClientGalleryService(), new RecordingAuditLogService());

        var result = await service.UploadVideoAsync(
            999,
            GalleryTestFiles.Create("intro.webm", "video/webm", [1, 2, 3]),
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private AdminGalleryMediaUploadService Create(
        DGVisionStudio.Infrastructure.Data.AppDbContext context,
        StubClientGalleryService gallery,
        RecordingAuditLogService audit) =>
        new(
            gallery,
            audit,
            NullLogger<AdminGalleryMediaUploadService>.Instance,
            context,
            new TestWebHostEnvironment
            {
                ContentRootPath = _root,
                WebRootPath = Path.Combine(_root, "wwwroot")
            },
            new ClientGalleryMapper());

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
