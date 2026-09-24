using DGVisionStudio.Infrastructure.Services;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DGVisionStudio.Tests.Infrastructure;

public sealed class CloudinaryStorageRefactorTests
{
    [Theory]
    [InlineData("uploads/client-galleries", "client-galleries")]
    [InlineData("/uploads/portfolio/", "portfolio")]
    [InlineData("", "")]
    public void PathService_NormalizesFolders(string input, string expected)
    {
        CloudinaryPathService.NormalizeFolder(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(" My_Photo.Name ", "my-photo-name")]
    [InlineData("already-clean", "already-clean")]
    public void PathService_SanitizesPublicIdNames(string input, string expected)
    {
        CloudinaryPathService.SanitizePublicId(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("gallery/photo.jpg", "gallery/photo")]
    [InlineData("gallery/photo.webp", "gallery/photo")]
    [InlineData("gallery/archive.zip", "gallery/archive.zip")]
    [InlineData("", "")]
    public void PathService_ExtractsRelativePublicIds(string input, string expected)
    {
        var paths = new CloudinaryPathService("dgvisionstudio/portfolio");

        paths.ExtractPublicId(input).Should().Be(expected);
    }

    [Fact]
    public void PathService_ExtractsCloudinaryUrlAndRemovesVersionAndExtension()
    {
        var paths = new CloudinaryPathService("dgvisionstudio/portfolio");

        var result = paths.ExtractPublicId(
            "https://res.cloudinary.com/demo/image/upload/v123456/dgvisionstudio/portfolio/photo.jpg");

        result.Should().Be("dgvisionstudio/portfolio/photo");
    }

    [Fact]
    public void PathService_BuildsIdUnderConfiguredBaseFolder()
    {
        var paths = new CloudinaryPathService(" custom/root/ ");

        var result = paths.BuildPublicId("uploads/client-galleries", " My_Photo ");

        result.Should().StartWith("custom/root/client-galleries/my-photo-");
        result.Split('/').Last().Length.Should().BeGreaterThan("my-photo-".Length);
    }

    [Fact]
    public async Task ImageOptimizer_ProducesWebpAtRequestedMaximumWidth()
    {
        using var image = new Image<Rgba32>(1200, 600);
        await using var source = new MemoryStream();
        await image.SaveAsPngAsync(source);
        source.Position = 0;
        var optimizer = new CloudinaryImageOptimizer();

        await using var optimized = await optimizer.OptimizeAsync(source, 400, 82);
        using var result = await Image.LoadAsync(optimized);

        result.Width.Should().Be(400);
        result.Height.Should().Be(200);
        optimized.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FileStorageService_DelegatesUploadWithNormalizedPublicId()
    {
        var client = new RecordingCloudinaryStorageClient();
        var service = new CloudinaryFileStorageService(
            client,
            new CloudinaryImageOptimizer(),
            new CloudinaryPathService("dgvisionstudio/portfolio"));

        await using var input = new MemoryStream([1, 2, 3]);
        var url = await service.SaveImageAsync(
            input,
            "Photo.JPG",
            "uploads/client-galleries");

        url.Should().Be("https://cdn.example.test/image.jpg");
        client.Uploads.Should().ContainSingle();
        client.Uploads[0].FileName.Should().Be("Photo.JPG");
        client.Uploads[0].PublicId.Should()
            .StartWith("dgvisionstudio/portfolio/client-galleries/photo-");
    }

    [Fact]
    public async Task FileStorageService_UsesParsedPublicIdForDeleteAndExists()
    {
        var client = new RecordingCloudinaryStorageClient { Exists = true };
        var service = new CloudinaryFileStorageService(
            client,
            new CloudinaryImageOptimizer(),
            new CloudinaryPathService("dgvisionstudio/portfolio"));
        const string url =
            "https://res.cloudinary.com/demo/image/upload/v10/dgvisionstudio/portfolio/photo.webp";

        (await service.FileExistsAsync(url)).Should().BeTrue();
        await service.DeleteFileAsync(url);

        client.ExistsQueries.Should().Equal("dgvisionstudio/portfolio/photo");
        client.Deletes.Should().Equal("dgvisionstudio/portfolio/photo");
    }

    [Fact]
    public async Task FileStorageService_HandlesEmptyPathsWithoutCallingClient()
    {
        var client = new RecordingCloudinaryStorageClient();
        var service = new CloudinaryFileStorageService(
            client,
            new CloudinaryImageOptimizer(),
            new CloudinaryPathService("root"));

        (await service.FileExistsAsync(" ")).Should().BeFalse();
        (await service.OpenReadAsync(" ")).Should().BeNull();
        await service.DeleteFileAsync(" ");

        client.Deletes.Should().BeEmpty();
        client.ExistsQueries.Should().BeEmpty();
        client.OpenReads.Should().BeEmpty();
    }

    private sealed class RecordingCloudinaryStorageClient : ICloudinaryStorageClient
    {
        public List<(string FileName, string PublicId)> Uploads { get; } = [];
        public List<string> Deletes { get; } = [];
        public List<string> ExistsQueries { get; } = [];
        public List<string> OpenReads { get; } = [];
        public bool Exists { get; init; }

        public Task<string> UploadImageAsync(
            Stream stream,
            string fileName,
            string publicId,
            int maxWidth,
            int quality,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add((fileName, publicId));
            return Task.FromResult("https://cdn.example.test/image.jpg");
        }

        public Task DeleteImageAsync(
            string publicId,
            CancellationToken cancellationToken = default)
        {
            Deletes.Add(publicId);
            return Task.CompletedTask;
        }

        public Task<bool> ImageExistsAsync(
            string publicId,
            CancellationToken cancellationToken = default)
        {
            ExistsQueries.Add(publicId);
            return Task.FromResult(Exists);
        }

        public Task<Stream?> OpenReadAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            OpenReads.Add(url);
            return Task.FromResult<Stream?>(new MemoryStream([1]));
        }
    }
}
