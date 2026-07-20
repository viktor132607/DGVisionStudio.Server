using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace DGVisionStudio.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;

    public FileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        var targetDirectory = ResolveDirectoryPath(folderPath);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, generatedFileName);

        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(output, cancellationToken);

        return GetRelativeUrl(fullPath);
    }

    public async Task<string> SaveImageAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        int maxWidth = 2400,
        int quality = 82,
        CancellationToken cancellationToken = default)
    {
        var targetDirectory = ResolveDirectoryPath(folderPath);
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

        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        if (extension is ".jpg" or ".jpeg")
        {
            await image.SaveAsJpegAsync(output, new JpegEncoder
            {
                Quality = quality
            }, cancellationToken);
        }
        else if (extension == ".png")
        {
            await image.SaveAsPngAsync(output, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            }, cancellationToken);
        }
        else if (extension == ".webp")
        {
            await image.SaveAsWebpAsync(output, new WebpEncoder
            {
                Quality = quality
            }, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Unsupported image format.");
        }

        return GetRelativeUrl(fullPath);
    }

    public Task DeleteFileAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolvePath(relativePath, out var fullPath))
            return Task.CompletedTask;

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolvePath(relativePath, out var fullPath) || !File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> FileExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            TryResolvePath(relativePath, out var fullPath) && File.Exists(fullPath));
    }

    private string ResolveDirectoryPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return GetWebRootPath();

        if (!TryResolvePath(folderPath, out var fullPath))
            throw new ArgumentException("Folder path must stay within the configured web root.", nameof(folderPath));

        return fullPath;
    }

    private bool TryResolvePath(string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var normalizedRelativePath = relativePath
            .TrimStart('/', '\\')
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedRelativePath))
            return false;

        var webRootPath = GetWebRootPath();
        var candidatePath = Path.GetFullPath(Path.Combine(webRootPath, normalizedRelativePath));
        var webRootPrefix = webRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidatePath.Equals(webRootPath, comparison) &&
            !candidatePath.StartsWith(webRootPrefix, comparison))
        {
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    private string GetWebRootPath()
    {
        var configuredPath = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        return Path.GetFullPath(configuredPath);
    }

    private string GetRelativeUrl(string fullPath)
    {
        var relativePath = Path.GetRelativePath(GetWebRootPath(), fullPath)
            .Replace('\\', '/');

        return "/" + relativePath;
    }
}
