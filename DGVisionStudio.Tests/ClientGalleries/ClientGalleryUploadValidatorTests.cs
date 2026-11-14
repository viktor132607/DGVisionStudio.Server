using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryUploadValidatorTests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/pjpeg")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    public async Task ValidateUploadedImageAsync_AllowsJpeg_WhenSignatureIsValid_EvenWithBrowserSpecificMimeType(string contentType)
    {
        var validator = new ClientGalleryUploadValidator();
        var file = CreateFile("photo.jpeg", contentType, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        var act = async () => await validator.ValidateUploadedImageAsync(file);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateUploadedImageAsync_RejectsFakeJpeg_EvenWithAllowedMimeType()
    {
        var validator = new ClientGalleryUploadValidator();
        var file = CreateFile("photo.jpeg", "image/jpeg", [0x25, 0x50, 0x44, 0x46]);

        var act = async () => await validator.ValidateUploadedImageAsync(file);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid image file signature.");
    }

    [Fact]
    public async Task ValidateUploadedImageAsync_RejectsJpeg_WhenMimeTypeIsClearlyWrong()
    {
        var validator = new ClientGalleryUploadValidator();
        var file = CreateFile("photo.jpeg", "application/pdf", [0xFF, 0xD8, 0xFF, 0xE0]);

        var act = async () => await validator.ValidateUploadedImageAsync(file);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid file MIME type.");
    }

    [Fact]
    public async Task ValidateUploadedImageAsync_ReadsSignatureAcrossShortStreamReads()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var file = new FormFile(new ShortReadStream(bytes), 0, bytes.Length, "file", "photo.png")
        {
            Headers = new HeaderDictionary(), ContentType = "image/png"
        };
        await new ClientGalleryUploadValidator().ValidateUploadedImageAsync(file);
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(2, buffer.Length)], cancellationToken);
    }

    private static IFormFile CreateFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
