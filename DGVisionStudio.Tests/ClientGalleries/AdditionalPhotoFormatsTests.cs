using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class AdditionalPhotoFormatsTests
{
    [Theory]
    [InlineData("bmp", "image/bmp")]
    [InlineData("bmp", "image/x-ms-bmp")]
    [InlineData("gif", "image/gif")]
    [InlineData("gif", "")]
    [InlineData("bmp", "application/octet-stream")]
    public async Task UploadDatabaseStorageAndDownload_PreserveRealFileBytesAndAnimation(string extension, string mime)
    {
        var bytes = await CreateImage(extension);
        var root = Path.Combine(Path.GetTempPath(), "dg-formats-" + Guid.NewGuid());
        try
        {
            await using var fixture = await GallerySqliteFixture.CreateAsync();
            var album = new PortfolioAlbum { Title = "Formats", Slug = "formats",
                PortfolioCategory = new PortfolioCategory { Key = "formats", Name = "Formats", NameEn = "Formats" } };
            fixture.Context.Add(album);
            await fixture.Context.SaveChangesAsync();
            var storage = new FileStorageService(new TestWebHostEnvironment { WebRootPath = root });
            var mapper = new ClientGalleryMapper();
            var uploads = new ClientGalleryPhotoUploadService(fixture.Context, storage, mapper,
                new ClientGalleryUploadValidator(), NullLogger<ClientGalleryPhotoUploadService>.Instance);
            var result = await uploads.UploadPhotoAsync(album.Id,
                GalleryTestFiles.Create("REAL." + extension.ToUpperInvariant(), mime, bytes));
            result.Should().NotBeNull();
            var stored = await fixture.Context.PortfolioImages.SingleAsync();
            stored.ImageUrl.Should().EndWith("." + extension);
            var downloads = new ClientGalleryPhotoDownloadService(fixture.Context, storage, mapper,
                new ClientGalleryNamingService(fixture.Context), NullLogger<ClientGalleryPhotoDownloadService>.Instance);
            var download = await downloads.OpenPhotoDownloadAsync(album.Id, stored.Id, "", true);
            download.Should().NotBeNull();
            download!.Value.ContentType.Should().Be("image/" + extension);
            await using var stream = download.Value.Stream;
            using var output = new MemoryStream();
            await stream.CopyToAsync(output);
            output.ToArray().Should().Equal(bytes);
            using var decoded = Image.Load<Rgba32>(output.ToArray());
            decoded.Width.Should().Be(32);
            decoded.Height.Should().Be(16);
            decoded.Frames.Count.Should().Be(extension == "gif" ? 2 : 1);
            decoded[0, 0].R.Should().Be(255);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData("gif")]
    public async Task Validation_RejectsRealFileRenamedToWrongFormat(string extension)
    {
        var bytes = await CreateImage(extension);
        var wrong = extension == "bmp" ? "gif" : "bmp";
        var act = () => new ClientGalleryUploadValidator().ValidateUploadedImageAsync(
            GalleryTestFiles.Create("renamed." + wrong, "image/" + wrong, bytes));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid image file signature.");
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData("gif")]
    public async Task Validation_RejectsTruncatedFilesWithValidHeaders(string extension)
    {
        var bytes = (await CreateImage(extension))[..16];
        var act = () => new ClientGalleryUploadValidator().ValidateUploadedImageAsync(
            GalleryTestFiles.Create("broken." + extension, "image/" + extension, bytes));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid or damaged image file.");
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData("gif")]
    public async Task Validation_RejectsOversizeInsteadOfConverting(string extension)
    {
        var bytes = new byte[10 * 1024 * 1024 + 1];
        var act = () => new ClientGalleryUploadValidator().ValidateUploadedImageAsync(
            GalleryTestFiles.Create("large." + extension, "image/" + extension, bytes));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*10 MB*");
    }

    private static async Task<byte[]> CreateImage(string extension)
    {
        using var image = new Image<Rgba32>(32, 16, new Rgba32(255, 0, 0));
        using var output = new MemoryStream();
        if (extension == "gif")
        {
            image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;
            using var next = new Image<Rgba32>(32, 16, new Rgba32(0, 0, 255));
            image.Frames.AddFrame(next.Frames.RootFrame);
            image.Frames[1].Metadata.GetGifMetadata().FrameDelay = 20;
            await image.SaveAsGifAsync(output);
        }
        else await image.SaveAsBmpAsync(output);
        return output.ToArray();
    }
}
