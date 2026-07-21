using System.IO.Compression;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DGVisionStudio.Tests.Coverage;

public sealed class AdminClientGalleryExportAdditionalTests
{
    [Fact]
    public async Task DownloadAllAlbumsAsync_ReturnsNotFoundWhenThereAreNoAlbums()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new AdminClientGalleryExportService(
            fixture.Context,
            new StubFileStorageService(),
            NullLogger<AdminClientGalleryExportService>.Instance);

        var result = await service.DownloadAllAlbumsAsync(Context());

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DownloadAllAlbumsAsync_ReturnsNotFoundWhenEveryPhotoIsUnavailable()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var (_, album) = AddAlbum(fixture.Context, "Category", "Album");
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/uploads/missing.jpg",
            DisplayOrder = 1,
            IsPublished = true
        });
        await fixture.Context.SaveChangesAsync();
        var storage = new StubFileStorageService
        {
            OpenReadException = new IOException("storage unavailable")
        };
        var service = new AdminClientGalleryExportService(
            fixture.Context,
            storage,
            NullLogger<AdminClientGalleryExportService>.Instance);

        var result = await service.DownloadAllAlbumsAsync(Context());

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DownloadAllAlbumsAsync_CreatesZipWithSafeNamesAndExpectedExtensions()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var (_, album) = AddAlbum(fixture.Context, "Client/Photos", " / ");
        var jpg = new PortfolioImage
        {
            ImageUrl = "/uploads/photo.jpeg?version=1",
            DisplayOrder = 2,
            IsPublished = true
        };
        var fallbackExtension = new PortfolioImage
        {
            ImageUrl = "/uploads/photo.extension-that-is-too-long",
            DisplayOrder = 1,
            IsPublished = true
        };
        album.Images.Add(jpg);
        album.Images.Add(fallbackExtension);
        await fixture.Context.SaveChangesAsync();
        var storage = new StubFileStorageService();
        storage.Files[jpg.ImageUrl] = [1, 2, 3];
        storage.Files[fallbackExtension.ImageUrl] = [4, 5, 6];
        var service = new AdminClientGalleryExportService(
            fixture.Context,
            storage,
            NullLogger<AdminClientGalleryExportService>.Instance);

        var result = await service.DownloadAllAlbumsAsync(Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var download = result.Value.Should().BeOfType<FileDownloadResult>().Subject;
        download.ContentType.Should().Be("application/zip");
        download.FileName.Should().Be("dgvisionstudio-all-albums.zip");
        await using var stream = download.Stream;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        archive.Entries.Should().HaveCount(2);
        archive.Entries.Select(x => x.FullName).Should().OnlyContain(x => x.StartsWith("Client-Photos/"));
        archive.Entries.Select(x => Path.GetExtension(x.FullName)).Should().BeEquivalentTo([".jpg", ".jpeg"]);
        archive.Entries.Select(x => x.Length).Should().OnlyContain(x => x == 3);
    }

    [Fact]
    public async Task DownloadAllAlbumsAsync_ReturnsErrorWhenDatabaseContextIsDisposed()
    {
        var fixture = await GallerySqliteFixture.CreateAsync();
        var service = new AdminClientGalleryExportService(
            fixture.Context,
            new StubFileStorageService(),
            NullLogger<AdminClientGalleryExportService>.Instance);
        await fixture.DisposeAsync();

        var result = await service.DownloadAllAlbumsAsync(Context());

        result.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    private static (PortfolioCategory Category, PortfolioAlbum Album) AddAlbum(
        DGVisionStudio.Infrastructure.Data.AppDbContext context,
        string categoryName,
        string albumTitle)
    {
        var category = new PortfolioCategory
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = categoryName,
            NameEn = categoryName,
            DisplayOrder = 1,
            IsActive = true
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = Guid.NewGuid().ToString("N"),
            Title = albumTitle,
            DisplayOrder = 1,
            IsPublished = true,
            AllowClientAccess = true,
            IsUserUploaded = false
        };
        context.AddRange(category, album);
        return (category, album);
    }

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace-export");
}

public sealed class FileStorageImageCoverageTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.png")]
    [InlineData("photo.webp")]
    public async Task SaveImageAsync_EncodesSupportedFormatsAndResizesWideImages(string fileName)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var service = CreateService(root, useConfiguredWebRoot: true);
            await using var input = await CreatePngStreamAsync(20, 10);

            var relativePath = await service.SaveImageAsync(
                input,
                fileName,
                "uploads/images",
                maxWidth: 5,
                quality: 70);

            relativePath.Should().StartWith("/uploads/images/");
            var fullPath = Path.Combine(root, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            File.Exists(fullPath).Should().BeTrue();
            using var saved = await Image.LoadAsync(fullPath);
            saved.Width.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(5);
            saved.Height.Should().BeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveImageAsync_ThrowsForUnsupportedExtensionAfterLoadingImage()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var service = CreateService(root, useConfiguredWebRoot: true);
            await using var input = await CreatePngStreamAsync(4, 4);

            var action = () => service.SaveImageAsync(input, "photo.gif", "uploads/images");

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Unsupported image format.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveFileAsync_UsesContentRootWwwrootFallbackAndSupportsBlankFolder()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var service = CreateService(root, useConfiguredWebRoot: false);
            await using var input = new MemoryStream([7, 8, 9]);

            var relativePath = await service.SaveFileAsync(input, "document.bin", " ");

            relativePath.Should().MatchRegex("^/[a-f0-9]{32}\\.bin$");
            var fullPath = Path.Combine(root, "wwwroot", relativePath.TrimStart('/'));
            (await File.ReadAllBytesAsync(fullPath)).Should().Equal(7, 8, 9);
            (await service.FileExistsAsync(relativePath)).Should().BeTrue();
            await using var opened = await service.OpenReadAsync(relativePath);
            opened.Should().NotBeNull();
            await service.DeleteFileAsync(relativePath);
            File.Exists(fullPath).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveImageAsync_PreservesOriginalDimensionsWhenMaxWidthIsDisabled()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var service = CreateService(root, useConfiguredWebRoot: true);
            await using var input = await CreatePngStreamAsync(12, 7);

            var relativePath = await service.SaveImageAsync(input, "photo.jpg", "uploads", maxWidth: 0);

            var fullPath = Path.Combine(root, relativePath.TrimStart('/'));
            using var saved = await Image.LoadAsync(fullPath);
            saved.Width.Should().Be(12);
            saved.Height.Should().Be(7);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FileStorageService CreateService(string root, bool useConfiguredWebRoot) => new(
        new TestWebHostEnvironment
        {
            WebRootPath = useConfiguredWebRoot ? root : string.Empty,
            ContentRootPath = root
        });

    private static async Task<MemoryStream> CreatePngStreamAsync(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(20, 40, 60));
        var stream = new MemoryStream();
        await image.SaveAsync(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dgvision-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
