using System.Reflection;
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
    public void NormalizeCloudinaryFolder_HandlesUrlsSlashesAndEmptyValues(string input, string expected)
    {
        InvokeStatic<string>("NormalizeCloudinaryFolder", input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  My.File_Name  ", "my-file-name")]
    [InlineData("---", null)]
    public void SanitizePublicId_NormalizesNamesAndFallsBackForEmptyResults(string input, string? expected)
    {
        var result = InvokeStatic<string>("SanitizePublicId", input);

        if (expected is null)
            result.Should().MatchRegex("^[a-f0-9]{32}$");
        else
            result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v123/folder/photo.jpg", "folder/photo")]
    [InlineData("folder/photo.webp", "folder/photo")]
    [InlineData("folder/document.txt", "folder/document.txt")]
    public void ExtractPublicId_StripsCloudinaryUploadPrefixVersionAndKnownExtensions(
        string input,
        string expected)
    {
        var service = CreateService();
        var method = typeof(CloudinaryFileStorageService).GetMethod(
            "ExtractPublicId",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(service, [input]).Should().Be(expected);
    }

    [Fact]
    public async Task OversizedImageOptimization_ProducesUploadableWebp()
    {
        using var image = new Image<Rgba32>(3000, 2000, new Rgba32(20, 40, 60));
        await using var source = new MemoryStream();
        await image.SaveAsPngAsync(source);
        source.Position = 0;

        var method = typeof(CloudinaryFileStorageService).GetMethod(
            "OptimizeImageForCloudinaryAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var task = (Task<MemoryStream>)method.Invoke(
            null,
            [source, 1200, 82, CancellationToken.None])!;

        await using var optimizedStream = await task;
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

    private static T InvokeStatic<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(CloudinaryFileStorageService).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (T)method.Invoke(null, arguments)!;
    }
}
