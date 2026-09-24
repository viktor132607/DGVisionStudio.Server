using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class CloudinaryImageOptimizer
{
    private const long TargetSizeBytes = 9 * 1024 * 1024;
    private const int MinimumOptimizedWidth = 960;

    public async Task<MemoryStream> OptimizeAsync(
        Stream fileStream,
        int maxWidth,
        int quality,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (fileStream.CanSeek)
            fileStream.Position = 0;

        using var image = await ImageSharpImage.LoadAsync(fileStream, cancellationToken);
        image.Mutate(x => x.AutoOrient());
        RemoveMetadata(image);

        var targetWidth = Math.Min(image.Width, maxWidth > 0 ? maxWidth : 2400);
        var targetQuality = Math.Clamp(quality, 45, 90);

        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var candidate = image.Clone(context =>
            {
                if (targetWidth > 0 && image.Width > targetWidth)
                {
                    context.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(targetWidth, 0)
                    });
                }
            });

            var output = new MemoryStream();
            await candidate.SaveAsWebpAsync(
                output,
                new WebpEncoder { Quality = targetQuality },
                cancellationToken);

            if (output.Length <= TargetSizeBytes)
            {
                output.Position = 0;
                return output;
            }

            await output.DisposeAsync();

            if (targetQuality > 55)
            {
                targetQuality -= 10;
                continue;
            }

            if (targetWidth > MinimumOptimizedWidth)
            {
                targetWidth = Math.Max(
                    MinimumOptimizedWidth,
                    (int)Math.Round(targetWidth * 0.8));
                continue;
            }

            break;
        }

        throw new InvalidOperationException(
            "The image could not be optimized below the Cloudinary upload limit.");
    }

    private static void RemoveMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
    }
}
