using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services;

public static class VideoUploadValidation
{
    public const long MaxFileSizeBytes = 500L * 1024 * 1024;
    public const long MaxRequestSizeBytes = 505L * 1024 * 1024;
    public const string MaxFileSizeLabel = "500MB";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov",
        ".webm",
        ".m4v"
    };

    public static async Task<bool> IsAllowedAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return false;

        if (HasDeclaredVideoContentType(file.ContentType))
            return true;

        if (!HasMissingOrGenericContentType(file.ContentType))
            return false;

        var header = new byte[64];
        await using var stream = file.OpenReadStream();
        var bytesRead = await ReadHeaderAsync(stream, header, cancellationToken);

        if (extension.Equals(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return bytesRead >= 4 &&
                header[0] == 0x1A &&
                header[1] == 0x45 &&
                header[2] == 0xDF &&
                header[3] == 0xA3;
        }

        return ContainsIsoBaseMediaFileTypeBox(header, bytesRead);
    }

    private static bool HasDeclaredVideoContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
         contentType.Equals("application/quicktime", StringComparison.OrdinalIgnoreCase));

    private static bool HasMissingOrGenericContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ||
        contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIsoBaseMediaFileTypeBox(byte[] header, int bytesRead)
    {
        for (var index = 4; index <= bytesRead - 4; index++)
        {
            if (header[index] == (byte)'f' &&
                header[index + 1] == (byte)'t' &&
                header[index + 2] == (byte)'y' &&
                header[index + 3] == (byte)'p')
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<int> ReadHeaderAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);

            if (read == 0)
                break;

            totalRead += read;
        }

        return totalRead;
    }
}
