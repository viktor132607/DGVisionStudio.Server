using DGVisionStudio.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Infrastructure.Services;

public class CloudinaryFileStorageService : IFileStorageService
{
    private const long CloudinaryMaxImageUploadSizeBytes = 10 * 1024 * 1024;

    private readonly ICloudinaryStorageClient client;
    private readonly CloudinaryImageOptimizer imageOptimizer;
    private readonly CloudinaryPathService pathService;

    public CloudinaryFileStorageService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        client = new CloudinaryStorageClient(configuration);
        imageOptimizer = new CloudinaryImageOptimizer();
        pathService = new CloudinaryPathService(configuration);
    }

    public CloudinaryFileStorageService(
        ICloudinaryStorageClient client,
        CloudinaryImageOptimizer imageOptimizer,
        CloudinaryPathService pathService)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.imageOptimizer = imageOptimizer ?? throw new ArgumentNullException(nameof(imageOptimizer));
        this.pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    public Task<string> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        CancellationToken cancellationToken = default) =>
        SaveImageAsync(
            fileStream,
            fileName,
            folderPath,
            cancellationToken: cancellationToken);

    public async Task<string> SaveImageAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        int maxWidth = 2400,
        int quality = 82,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            throw new InvalidOperationException("Unsupported image format.");

        MemoryStream? bufferedStream = null;
        MemoryStream? optimizedStream = null;
        Stream uploadStream = fileStream;
        var uploadFileName = fileName;

        try
        {
            if (!uploadStream.CanSeek)
            {
                bufferedStream = new MemoryStream();
                await uploadStream.CopyToAsync(bufferedStream, cancellationToken);
                bufferedStream.Position = 0;
                uploadStream = bufferedStream;
            }
            else
            {
                uploadStream.Position = 0;
            }

            if (uploadStream.Length > CloudinaryMaxImageUploadSizeBytes)
            {
                optimizedStream = await imageOptimizer.OptimizeAsync(
                    uploadStream,
                    maxWidth,
                    quality,
                    cancellationToken);

                uploadStream = optimizedStream;
                uploadFileName = Path.ChangeExtension(fileName, ".webp");
            }

            var publicId = pathService.BuildPublicId(
                folderPath,
                Path.GetFileNameWithoutExtension(fileName));

            return await client.UploadImageAsync(
                uploadStream,
                uploadFileName,
                publicId,
                maxWidth,
                quality,
                cancellationToken);
        }
        finally
        {
            if (optimizedStream != null)
                await optimizedStream.DisposeAsync();

            if (bufferedStream != null)
                await bufferedStream.DisposeAsync();
        }
    }

    public Task DeleteFileAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var publicId = pathService.ExtractPublicId(relativePath);

        return string.IsNullOrWhiteSpace(publicId)
            ? Task.CompletedTask
            : client.DeleteImageAsync(publicId, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? Task.FromResult<Stream?>(null)
            : client.OpenReadAsync(relativePath, cancellationToken);

    public async Task<bool> FileExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var publicId = pathService.ExtractPublicId(relativePath);

        return !string.IsNullOrWhiteSpace(publicId) &&
               await client.ImageExistsAsync(publicId, cancellationToken);
    }
}
