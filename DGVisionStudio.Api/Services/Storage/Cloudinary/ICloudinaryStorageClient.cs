namespace DGVisionStudio.Infrastructure.Services;

public interface ICloudinaryStorageClient
{
    Task<string> UploadImageAsync(
        Stream stream,
        string fileName,
        string publicId,
        int maxWidth,
        int quality,
        CancellationToken cancellationToken = default);

    Task DeleteImageAsync(
        string publicId,
        CancellationToken cancellationToken = default);

    Task<bool> ImageExistsAsync(
        string publicId,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string url,
        CancellationToken cancellationToken = default);
}
