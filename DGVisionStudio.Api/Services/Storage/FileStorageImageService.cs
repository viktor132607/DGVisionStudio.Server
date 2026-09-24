using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class FileStorageImageService
{
    private readonly FileStoragePathService _paths;

    public FileStorageImageService(FileStoragePathService paths)
    {
        _paths = paths;
    }

    public async Task<string> SaveImageAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        int maxWidth = 2400,
        int quality = 82,
        CancellationToken cancellationToken = default)
    {
        var targetDirectory = _paths.ResolveDirectoryPath(folderPath);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, generatedFileName);

        using var image = await ImageSharpImage.LoadAsync(fileStream, cancellationToken);

        if (maxWidth > 0 && image.Width > maxWidth)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxWidth, 0)
            }));
        }

        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        if (extension is ".jpg" or ".jpeg")
        {
            await image.SaveAsJpegAsync(
                output,
                new JpegEncoder { Quality = quality },
                cancellationToken);
        }
        else if (extension == ".png")
        {
            await image.SaveAsPngAsync(
                output,
                new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression
                },
                cancellationToken);
        }
        else if (extension == ".webp")
        {
            await image.SaveAsWebpAsync(
                output,
                new WebpEncoder { Quality = quality },
                cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Unsupported image format.");
        }

        return _paths.GetRelativeUrl(fullPath);
    }
}
