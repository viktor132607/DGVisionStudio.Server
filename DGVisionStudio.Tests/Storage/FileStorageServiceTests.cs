using System.Text;
using DGVisionStudio.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace DGVisionStudio.Tests.Storage;

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"dgvision-storage-{Guid.NewGuid():N}");
    private readonly string _webRoot;
    private readonly FileStorageService _service;

    public FileStorageServiceTests()
    {
        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        Directory.CreateDirectory(_webRoot);
        _service = new FileStorageService(new TestWebHostEnvironment
        {
            ContentRootPath = _contentRoot,
            WebRootPath = _webRoot
        });
    }

    [Fact]
    public async Task SaveOpenExistsAndDeleteFile_UsesTheConfiguredWebRoot()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("stored content"));

        var relativePath = await _service.SaveFileAsync(input, "photo.txt", "uploads/test");

        relativePath.Should().StartWith("/uploads/test/");
        (await _service.FileExistsAsync(relativePath)).Should().BeTrue();

        await using var stored = await _service.OpenReadAsync(relativePath);
        stored.Should().NotBeNull();
        using var reader = new StreamReader(stored!, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("stored content");

        await _service.DeleteFileAsync(relativePath);
        (await _service.FileExistsAsync(relativePath)).Should().BeFalse();
    }

    [Fact]
    public async Task SaveFileAsync_RejectsFolderTraversalOutsideWebRoot()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("blocked"));

        var action = () => _service.SaveFileAsync(input, "blocked.txt", "../outside");

        await action.Should().ThrowAsync<ArgumentException>();
        Directory.Exists(Path.Combine(_contentRoot, "outside")).Should().BeFalse();
    }

    [Fact]
    public async Task ReadExistsAndDelete_RejectPathsOutsideWebRoot()
    {
        var externalFile = Path.Combine(_contentRoot, "secret.txt");
        await File.WriteAllTextAsync(externalFile, "secret");

        (await _service.OpenReadAsync("../secret.txt")).Should().BeNull();
        (await _service.FileExistsAsync("../secret.txt")).Should().BeFalse();
        await _service.DeleteFileAsync("../secret.txt");

        File.Exists(externalFile).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
