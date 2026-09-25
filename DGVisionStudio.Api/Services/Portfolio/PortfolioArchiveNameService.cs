using System.Text;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioArchiveNameService
{
    public string CategorySegment(
        string? name,
        int id,
        ISet<string> used) =>
        Unique(
            SafeSegment(name, $"category-{id}", 60),
            id,
            used);

    public string AlbumSegment(
        string? title,
        int id,
        ISet<string> used) =>
        Unique(
            SafeSegment(title, $"album-{id}", 70),
            id,
            used);

    public string PhotoFileName(
        string? name,
        string? imageUrl,
        int id,
        ISet<string> used)
    {
        var extension = Path.GetExtension(
            (imageUrl ?? string.Empty).Split('?', '#')[0]);

        if (extension.Length is < 2 or > 10 ||
            extension.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c)))
        {
            extension = ".jpg";
        }

        var safeName = SafeSegment(name, $"photo-{id}", 90);
        if (safeName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            safeName = safeName[..^extension.Length];

        return Unique(safeName, id, used, extension);
    }

    private static string SafeSegment(
        string? value,
        string fallback,
        int maxLength)
    {
        var raw = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Normalize(NormalizationForm.FormC);

        var cleaned = new string(raw
            .Select(c =>
                char.IsControl(c) || "<>:"/\\|?*".Contains(c)
                    ? '-'
                    : c)
            .ToArray())
            .Trim(' ', '.');

        if (cleaned.Length > maxLength)
            cleaned = cleaned[..maxLength].TrimEnd(' ', '.');

        if (string.IsNullOrWhiteSpace(cleaned))
            return fallback;

        var stem = cleaned.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 &&
             (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
             char.IsDigit(stem[3])))
        {
            cleaned = "_" + cleaned;
        }

        return cleaned;
    }

    private static string Unique(
        string name,
        int id,
        ISet<string> used,
        string extension = "")
    {
        var candidate = name + extension;
        var suffix = 0;

        while (!used.Add(candidate))
        {
            candidate =
                $"{name}-{id}{(suffix++ == 0 ? "" : $"-{suffix}")}{extension}";
        }

        return candidate;
    }
}
