namespace DGVisionStudio.Api.Configuration;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;

    public int MaxFilesPerRequest { get; set; } = 100;

    public long MaxRequestSizeBytes => MaxFileSizeBytes * MaxFilesPerRequest;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? Provider { get; set; }

    public bool UseCloudinary => string.Equals(Provider, "Cloudinary", StringComparison.OrdinalIgnoreCase);
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string? Url { get; set; }

    public string[] AdditionalOrigins { get; set; } = [];

    public IReadOnlyList<string> GetAllowedOrigins()
    {
        var origins = new List<string>();
        AddOrigin(origins, Url);

        foreach (var origin in AdditionalOrigins)
            AddOrigin(origins, origin);

        return origins
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValidOrigin(string? origin)
    {
        return string.IsNullOrWhiteSpace(origin)
            || Uri.TryCreate(origin.TrimEnd('/'), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static void AddOrigin(List<string> origins, string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return;

        var normalized = origin.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(normalized))
            origins.Add(normalized);
    }
}
