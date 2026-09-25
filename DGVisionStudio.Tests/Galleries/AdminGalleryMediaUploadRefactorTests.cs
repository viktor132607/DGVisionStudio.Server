using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Galleries;

public sealed class AdminGalleryMediaUploadRefactorTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"dg-gallery-upload-refactor-{Guid.NewGuid():N}");

    [Fact]
    public async Task PhotoUploadService_ValidatesAndAuditsSuccessfulUpload()
    {
        var gallery = new StubClientGalleryService
        {
            UploadPhoto = (_, file) => Task.FromResult<ClientPhotoDto?>(
                new ClientPhotoDto { Id = file.FileName == "ok.jpg" ? 31 : 0 })
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryPhotoUploadService(
            gallery,
            audit,
            NullLogger<AdminGalleryMediaUploadService>.Instance);

        var invalidGallery = await service.UploadAsync(
            0,
            GalleryTestFiles.Create("ok.jpg", "image/jpeg"),
            Context());
        var invalidContent = await service.UploadAsync(
            1,
            GalleryTestFiles.Create("notes.txt", "text/plain"),
            Context());
        var uploaded = await service.UploadAsync(
            1,
            GalleryTestFiles.Create("ok.jpg", "image/jpeg"),
            Context());

        invalidGallery.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        invalidContent.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        uploaded.StatusCode.Should().Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(x =>
            x.Action == "UploadGalleryPhoto" && x.EntityId == "31");
    }

    [Fact]
    public async Task VideoFileStorage_UsesContentRootFallbackAndPreservesRelativeUrl()
    {
        Directory.CreateDirectory(_root);
        var storage = new AdminGalleryVideoFileStorageService(
            new TestWebHostEnvironment
            {
                ContentRootPath = _root,
                WebRootPath = string.Empty
            });

        var savedPath = await storage.SaveAsync(
            GalleryTestFiles.Create("INTRO.MP4", "video/mp4", [1, 2, 3, 4]));

        savedPath.Should().MatchRegex(
            "^/uploads/portfolio/videos/[a-f0-9]{32}\\.mp4$");
        var fullPath = Path.Combine(
            _root,
            "wwwroot",
            savedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(fullPath)).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task VideoUploadService_PreservesDisplayOrderMappingAndAudit()
    {
        Directory.CreateDirectory(_root);
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory
        {
            Key = "video-refactor",
            Name = "Video",
            NameEn = "Video"
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "video-refactor",
            Title = "Video",
            IsDeleted = false
        };
        var existing = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/uploads/portfolio/existing.jpg",
            DisplayOrder = 7,
            IsDeleted = false,
            IsPublished = true
        };
        fixture.Context.AddRange(category, album, existing);
        await fixture.Context.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var service = new AdminGalleryVideoUploadService(
            audit,
            NullLogger<AdminGalleryMediaUploadService>.Instance,
            fixture.Context,
            new AdminGalleryVideoFileStorageService(
                new TestWebHostEnvironment
                {
                    ContentRootPath = _root,
                    WebRootPath = Path.Combine(_root, "wwwroot")
                }),
            new ClientGalleryMapper());

        var result = await service.UploadAsync(
            album.Id,
            GalleryTestFiles.Create(" intro.mp4", "video/mp4", [0, 0, 0, 24]),
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var dto = result.Value.Should().BeOfType<ClientPhotoDto>().Subject;
        dto.MediaType.Should().Be("Video");

        var video = fixture.Context.PortfolioImages
            .Single(x => x.Id == dto.Id);
        video.DisplayOrder.Should().Be(8);
        video.AltText.Should().Be(" intro");
        video.ImageUrl.Should().StartWith("/uploads/portfolio/videos/")
            .And.EndWith(".mp4");
        audit.Entries.Should().ContainSingle(x =>
            x.Action == "UploadGalleryVideo" &&
            x.EntityId == video.Id.ToString());
    }

    [Fact]
    public async Task Facade_DelegatesToFocusedUploadServices()
    {
        Directory.CreateDirectory(_root);
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var gallery = new StubClientGalleryService
        {
            UploadPhoto = (_, _) => Task.FromResult<ClientPhotoDto?>(
                new ClientPhotoDto { Id = 44 })
        };
        var audit = new RecordingAuditLogService();
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = _root,
            WebRootPath = Path.Combine(_root, "wwwroot")
        };
        var facade = new AdminGalleryMediaUploadService(
            new AdminGalleryPhotoUploadService(
                gallery,
                audit,
                NullLogger<AdminGalleryMediaUploadService>.Instance),
            new AdminGalleryVideoUploadService(
                audit,
                NullLogger<AdminGalleryMediaUploadService>.Instance,
                fixture.Context,
                new AdminGalleryVideoFileStorageService(environment),
                new ClientGalleryMapper()));

        var result = await facade.UploadPhotoAsync(
            1,
            GalleryTestFiles.Create("ok.jpg", "image/jpeg"),
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<ClientPhotoDto>()
            .Which.Id.Should().Be(44);
    }

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
