using DGVisionStudio.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Tests.Media;

public sealed class VideoUploadValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("application/octet-stream")]
    [InlineData("application/quicktime")]
    [InlineData("video/quicktime")]
    public async Task IsAllowedAsync_AcceptsIphoneMovWithCompatibleOrMissingMimeType(string contentType)
    {
        var file = CreateFile("IMG_1234.MOV", contentType, CreateIsoBaseMediaHeader());

        var result = await VideoUploadValidation.IsAllowedAsync(file);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_AcceptsWebmHeader()
    {
        var file = CreateFile(
            "clip.webm",
            "video/webm",
            [0x1A, 0x45, 0xDF, 0xA3, 0x00, 0x00, 0x00, 0x00]);

        var result = await VideoUploadValidation.IsAllowedAsync(file);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_RejectsRenamedNonVideoFile()
    {
        var file = CreateFile("not-a-video.mov", "application/octet-stream", "not a video"u8.ToArray());

        var result = await VideoUploadValidation.IsAllowedAsync(file);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_RejectsUnsupportedExtension()
    {
        var file = CreateFile("clip.avi", "video/x-msvideo", CreateIsoBaseMediaHeader());

        var result = await VideoUploadValidation.IsAllowedAsync(file);

        result.Should().BeFalse();
    }

    private static FormFile CreateFile(string fileName, string contentType, byte[] bytes) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static byte[] CreateIsoBaseMediaHeader() =>
    [
        0x00, 0x00, 0x00, 0x18,
        (byte)'f', (byte)'t', (byte)'y', (byte)'p',
        (byte)'q', (byte)'t', (byte)' ', (byte)' ',
        0x00, 0x00, 0x00, 0x00
    ];
}
