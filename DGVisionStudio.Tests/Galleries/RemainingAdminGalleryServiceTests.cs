using System.IO.Compression;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Galleries;

public sealed class AdminClientGalleryQueryServiceTests
{
    [Fact]
    public async Task QueryMethods_ValidateIdsAndReturnGalleryCollection()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var galleryService = new StubClientGalleryService
        {
            GetAllGalleries = () => Task.FromResult<List<MyClientGalleryDto>>([])
        };
        var user = TestUsers.Create("client@example.com", "user-1");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var manager = new ConfigurableUserManager([user]) { UsersSource = context.Users };
        var service = new AdminClientGalleryQueryService(galleryService, manager);

        var all = await service.GetAllGalleriesAsync();
        var invalid = await service.GetGalleryByIdAsync(0);
        var users = await service.GetAvailableUsersAsync();

        all.StatusCode.Should().Be(StatusCodes.Status200OK);
        all.Value.Should().BeOfType<List<MyClientGalleryDto>>().Which.Should().BeEmpty();
        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        users.StatusCode.Should().Be(StatusCodes.Status200OK);
        users.Value.Should().BeAssignableTo<IEnumerable<object>>()
            .Which.Should().ContainSingle();
    }
}

public sealed class AdminClientGalleryCommandServiceTests
{
    [Fact]
    public async Task CreateGalleryAsync_ValidatesRequestAndAuditsSuccessfulCreation()
    {
        var galleryService = new StubClientGalleryService
        {
            CreateGallery = request => Task.FromResult(27)
        };
        var audit = new RecordingAuditLogService();
        var service = new AdminClientGalleryCommandService(
            galleryService,
            audit,
            NullLogger<AdminClientGalleryCommandService>.Instance);

        var missing = await service.CreateGalleryAsync(null, AdminContext());
        var created = await service.CreateGalleryAsync(
            new AdminCreateClientGalleryRequest { Title = "Client gallery" },
            AdminContext());

        missing.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        created.StatusCode.Should().Be(StatusCodes.Status200OK);
        audit.Entries.Should().ContainSingle(x =>
            x.Action == "CreateGallery" && x.EntityId == "27");
    }

    private static AdminRequestContext AdminContext() => new(
        "admin-1",
        "admin@example.com",
        "Admin",
        "127.0.0.1",
        "tests",
        "trace-1");
}

public sealed class AdminClientGalleryExportServiceTests
{
    [Fact]
    public async Task DownloadAllAlbumsAsync_ReturnsZipContainingStoredPhoto()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory
        {
            Key = "weddings",
            Name = "Weddings",
            NameEn = "Weddings",
            DisplayOrder = 1,
            IsActive = true
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "client-album",
            Title = "Client Album",
            DisplayOrder = 1,
            IsPublished = true
        };
        var image = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/uploads/client/photo.jpg",
            DisplayOrder = 1,
            IsPublished = true
        };
        fixture.Context.AddRange(category, album, image);
        await fixture.Context.SaveChangesAsync();
        var storage = new StubFileStorageService();
        storage.Files[image.ImageUrl] = [1, 2, 3, 4];
        var service = new AdminClientGalleryExportService(
            fixture.Context,
            storage,
            NullLogger<AdminClientGalleryExportService>.Instance);

        var result = await service.DownloadAllAlbumsAsync(AdminContext());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var download = result.Value.Should().BeOfType<FileDownloadResult>().Subject;
        download.ContentType.Should().Be("application/zip");
        using var archive = new ZipArchive(download.Stream, ZipArchiveMode.Read, leaveOpen: false);
        archive.Entries.Should().ContainSingle();
        archive.Entries[0].FullName.Should().EndWith(".jpg");
    }

    private static AdminRequestContext AdminContext() => new(
        "admin-1",
        "admin@example.com",
        "Admin",
        "127.0.0.1",
        "tests",
        "trace-1");
}

public sealed class AdminClientGalleryManagementServiceTests
{
    [Fact]
    public async Task Facade_DelegatesQueriesAndCommands()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var galleryService = new StubClientGalleryService
        {
            GetAllGalleries = () => Task.FromResult<List<MyClientGalleryDto>>([])
        };
        var manager = new ConfigurableUserManager();
        var audit = new RecordingAuditLogService();
        var storage = new StubFileStorageService();
        var service = new AdminClientGalleryManagementService(
            new AdminClientGalleryQueryService(galleryService, manager),
            new AdminClientGalleryCommandService(
                galleryService,
                audit,
                NullLogger<AdminClientGalleryCommandService>.Instance),
            new AdminClientGalleryExportService(
                context,
                storage,
                NullLogger<AdminClientGalleryExportService>.Instance));

        var all = await service.GetAllGalleriesAsync();
        var invalid = await service.GetGalleryByIdAsync(-1);

        all.StatusCode.Should().Be(StatusCodes.Status200OK);
        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
