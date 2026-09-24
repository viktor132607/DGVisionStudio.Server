using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class CloudinaryStorageClient : ICloudinaryStorageClient
{
    private static readonly HttpClient HttpClient = new();
    private readonly Cloudinary cloudinary;

    public CloudinaryStorageClient(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var cloudName = configuration["Cloudinary:CloudName"]?.Trim();
        var apiKey = configuration["Cloudinary:ApiKey"]?.Trim();
        var apiSecret = configuration["Cloudinary:ApiSecret"]?.Trim();

        if (string.IsNullOrWhiteSpace(cloudName))
            throw new InvalidOperationException("Cloudinary:CloudName is missing.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Cloudinary:ApiKey is missing.");

        if (string.IsNullOrWhiteSpace(apiSecret))
            throw new InvalidOperationException("Cloudinary:ApiSecret is missing.");

        cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret))
        {
            Api =
            {
                Secure = true
            }
        };
    }

    public async Task<string> UploadImageAsync(
        Stream stream,
        string fileName,
        string publicId,
        int maxWidth,
        int quality,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);

        cancellationToken.ThrowIfCancellationRequested();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            PublicId = publicId,
            Overwrite = false,
            UseFilename = false,
            UniqueFilename = true,
            Transformation = new Transformation()
                .Width(maxWidth)
                .Crop("limit")
                .Quality(quality)
        };

        var result = await cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed. Error: {result.Error.Message}");

        var secureUrl = result.SecureUrl?.ToString();
        if (string.IsNullOrWhiteSpace(secureUrl))
            throw new InvalidOperationException("Cloudinary upload failed. SecureUrl is empty.");

        return secureUrl;
    }

    public async Task DeleteImageAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await cloudinary.DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Image
        });

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary delete failed. Error: {result.Error.Message}");
    }

    public async Task<bool> ImageExistsAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await cloudinary.GetResourceAsync(new GetResourceParams(publicId));
            return result != null && result.Error == null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Stream?> OpenReadAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var memoryStream = new MemoryStream();
            await response.Content.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            return null;
        }
    }
}
