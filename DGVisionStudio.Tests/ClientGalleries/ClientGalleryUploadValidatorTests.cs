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
