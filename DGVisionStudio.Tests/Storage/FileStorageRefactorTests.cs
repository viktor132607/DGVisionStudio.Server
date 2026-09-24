using System.Text;
using DGVisionStudio.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DGVisionStudio.Tests.Storage;

public sealed class FileStorageRefactorTests : IDisposable
{
    private readonly string _contentRoot =
        Path.Combine(Path.GetTempPath(), $"dgvision-storage-refactor-{Guid.NewGuid():N}");
    private readonly string _webRoot;
    private readonly FileStoragePathService _paths;

    public FileStorageRefactorTests()
    {
        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        Directory.CreateDirectory(_webRoot);
        _paths = new FileStoragePathService(new TestWebHostEnvironment
        {
            ContentRootPath = _contentRoot,
            WebRootPath = _webRoot
        });
    }

    [Fact]
    public void PathService_ResolvesInsideWebRootAndRejectsTraversal()
    {
        _paths.ResolveDirectoryPath(" ")
            .Should().Be(Path.GetFullPath(_webRoot));

        _paths.TryResolvePath(
                "/uploads/images/photo.jpg",
                out var resolvedPath)
            .Should().BeTrue();

        resolvedPath.Should().Be(
            Path.GetFullPath(
                Path.Combine(
                    _webRoot,
                    "uploads",
                    "images",
                    "photo.jpg")));

        _paths.TryResolvePath("../outside.txt", out _)
            .Should().BeFalse();
    }

    [Fact]
    public async Task FileService_RoundTripsFileWithoutChangingStorageSemantics()
    {
        var service = new FileStorageFileService(_paths);
        await using var input =
            new MemoryStream(Encoding.UTF8.GetBytes("focused file service"));

        var relativePath = await service.SaveFileAsync(
            input,
            "PHOTO.TXT",
            "uploads/files");

        relativePath.Should().MatchRegex(
            "^/uploads/files/[a-f0-9]{32}\\.txt$");
        (await service.FileExistsAsync(relativePath)).Should().BeTrue();

        await using var stored = await service.OpenReadAsync(relativePath);
        stored.Should().NotBeNull();
        using var reader = new StreamReader(stored!, Encoding.UTF8);
        (await reader.ReadToEndAsync())
            .Should().Be("focused file service");

        await service.DeleteFileAsync(relativePath);
        (await service.FileExistsAsync(relativePath)).Should().BeFalse();
    }

    [Fact]
    public async Task ImageService_ResizesAndEncodesSupportedImage()
    {
        var service = new FileStorageImageService(_paths);
        await using var input = await CreatePngStreamAsync(20, 10);

        var relativePath = await service.SaveImageAsync(
            input,
            "photo.webp",
            "uploads/images",
            maxWidth: 5,
            quality: 70);

        relativePath.Should().MatchRegex(
            "^/uploads/images/[a-f0-9]{32}\\.webp$");

        var fullPath = Path.Combine(
            _webRoot,
            relativePath.TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar));

        using var saved = await Image.LoadAsync(fullPath);
        saved.Width.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(5);
        saved.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InjectedFacade_DelegatesFileAndImageOperations()
    {
        var facade = new FileStorageService(
            new FileStorageFileService(_paths),
            new FileStorageImageService(_paths));

        await using var fileInput = new MemoryStream([1, 2, 3]);
        var filePath = await facade.SaveFileAsync(
            fileInput,
            "document.bin",
            "uploads");

        (await facade.FileExistsAsync(filePath)).Should().BeTrue();

        await using var imageInput = await CreatePngStreamAsync(4, 4);
        var imagePath = await facade.SaveImageAsync(
            imageInput,
            "photo.png",
            "uploads");

        (await facade.FileExistsAsync(imagePath)).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    private static async Task<MemoryStream> CreatePngStreamAsync(
        int width,
        int height)
    {
        using var image = new Image<Rgba32>(
            width,
            height,
            new Rgba32(20, 40, 60));
        var stream = new MemoryStream();
        await image.SaveAsync(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
