using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class CloudinaryPathService
{
    private readonly string baseFolder;

    public CloudinaryPathService(IConfiguration configuration)
        : this(configuration?["Cloudinary:Folder"])
    {
    }

    public CloudinaryPathService(string? folder)
    {
        baseFolder = string.IsNullOrWhiteSpace(folder)
            ? "dgvisionstudio/portfolio"
            : folder.Trim().Trim('/');

        if (string.IsNullOrWhiteSpace(baseFolder))
            baseFolder = "dgvisionstudio/portfolio";
    }

    public string BuildPublicId(string? folderPath, string? fileNameWithoutExtension)
    {
        var safeFolder = NormalizeFolder(folderPath);
        var safeName = string.IsNullOrWhiteSpace(fileNameWithoutExtension)
            ? Guid.NewGuid().ToString("N")
            : SanitizePublicId(fileNameWithoutExtension);

        var uniqueName = $"{safeName}-{Guid.NewGuid():N}";

        return string.IsNullOrWhiteSpace(safeFolder)
            ? $"{baseFolder}/{uniqueName}"
            : $"{baseFolder}/{safeFolder}/{uniqueName}";
    }

    public string ExtractPublicId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var input = value.Trim();

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return StripKnownExtension(input.Trim('/'));

        var parts = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var uploadIndex = parts.FindIndex(
            x => x.Equals("upload", StringComparison.OrdinalIgnoreCase));

        if (uploadIndex >= 0)
            parts = parts.Skip(uploadIndex + 1).ToList();

        if (parts.Count > 0 &&
            parts[0].StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
            parts[0].Skip(1).All(char.IsDigit))
        {
            parts.RemoveAt(0);
        }

        return StripKnownExtension(string.Join("/", parts));
    }

    internal static string NormalizeFolder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var trimmed = value.Trim().Replace("\\", "/");

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            trimmed = uri.AbsolutePath;

        return trimmed.Trim('/').Replace("uploads/", "");
    }

    internal static string SanitizePublicId(string value)
    {
        var safe = value.Trim().ToLowerInvariant();

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalidChar, '-');

        safe = safe
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-");

        while (safe.Contains("--", StringComparison.Ordinal))
            safe = safe.Replace("--", "-");

        return string.IsNullOrWhiteSpace(safe.Trim('-'))
            ? Guid.NewGuid().ToString("N")
            : safe.Trim('-');
    }

    internal static string StripKnownExtension(string value)
    {
        var extension = Path.GetExtension(value);

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".pdf" =>
                value[..^extension.Length],
            _ => value
        };
    }
}
