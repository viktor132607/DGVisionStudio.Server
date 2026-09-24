using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DGVisionStudio.Tests.Coverage;

public sealed class CloudinaryFileStorageCoverageTests
{
    [Fact]
    public void Constructor_ValidatesEachRequiredCredential()
    {
        var missingKey = () => new CloudinaryFileStorageService(TestConfiguration.Create(
            ("Cloudinary:CloudName", "cloud")));
        var missingSecret = () => new CloudinaryFileStorageService(TestConfiguration.Create(
            ("Cloudinary:CloudName", "cloud"),
            ("Cloudinary:ApiKey", "key")));

        missingKey.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
        missingSecret.Should().Throw<InvalidOperationException>().WithMessage("*ApiSecret*");
    }

    [Fact]
    public async Task EmptyPaths_AreHandledWithoutExternalCloudinaryCalls()
    {
        var service = CreateService();

        (await service.OpenReadAsync(" ")).Should().BeNull();
        (await service.FileExistsAsync(" ")).Should().BeFalse();
        await service.DeleteFileAsync(" ");

        var unsupported = () => service.SaveFileAsync(
            new MemoryStream([1, 2, 3]),
            "payload.exe",
            "uploads/portfolio");
        await unsupported.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unsupported image format.");
    }

    [Theory]
    [InlineData("uploads/portfolio/albums", "portfolio/albums")]
    [InlineData("/uploads/portfolio/albums/", "portfolio/albums")]
    [InlineData("https://example.com/uploads/portfolio/albums", "portfolio/albums")]
    [InlineData("uploads\\portfolio\\albums", "portfolio/albums")]
    [InlineData(" ", "")]
    public void NormalizeCloudinaryFolder_HandlesUrlsSlashesAndEmptyValues(
        string input,
        string expected)
    {
        var paths = new CloudinaryPathService("root");

        var result = paths.BuildPublicId(input, "photo");

        var expectedPrefix = string.IsNullOrEmpty(expected)
            ? "root/photo-"
            : $"root/{expected}/photo-";

        result.Should().StartWith(expectedPrefix);
    }

    [Theory]
    [InlineData("  My.File_Name  ", "^root/my-file-name-[a-f0-9]{32}$")]
    [InlineData("---", "^root/[a-f0-9]{32}-[a-f0-9]{32}$")]
    public void SanitizePublicId_NormalizesNamesAndFallsBackForEmptyResults(
        string input,
        string expectedPattern)
    {
        var paths = new CloudinaryPathService("root");

        paths.BuildPublicId("", input).Should().MatchRegex(expectedPattern);
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v123/folder/photo.jpg", "folder/photo")]
    [InlineData("folder/photo.webp", "folder/photo")]
    [InlineData("folder/document.txt", "folder/document.txt")]
    public void ExtractPublicId_StripsCloudinaryUploadPrefixVersionAndKnownExtensions(
        string input,
        string expected)
    {
        var paths = new CloudinaryPathService("root");

        paths.ExtractPublicId(input).Should().Be(expected);
    }

    [Fact]
    public async Task OversizedImageOptimization_ProducesUploadableWebp()
    {
        using var image = new Image<Rgba32>(3000, 2000, new Rgba32(20, 40, 60));
        await using var source = new MemoryStream();
        await image.SaveAsPngAsync(source);
        source.Position = 0;

        var optimizer = new CloudinaryImageOptimizer();

        await using var optimizedStream = await optimizer.OptimizeAsync(
            source,
            1200,
            82,
            CancellationToken.None);

        optimizedStream.Length.Should().BeLessThanOrEqualTo(9 * 1024 * 1024);
        optimizedStream.Position = 0;

        using var optimizedImage = await Image.LoadAsync(optimizedStream);
        optimizedImage.Width.Should().BeLessThanOrEqualTo(1200);
    }

    private static CloudinaryFileStorageService CreateService() => new(
        TestConfiguration.Create(
            ("Cloudinary:CloudName", "test-cloud"),
            ("Cloudinary:ApiKey", "test-key"),
            ("Cloudinary:ApiSecret", "test-secret"),
            ("Cloudinary:Folder", " /dgvisionstudio/portfolio/ ")));
}
