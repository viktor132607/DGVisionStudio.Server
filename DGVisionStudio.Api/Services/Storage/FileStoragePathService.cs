using Microsoft.AspNetCore.Hosting;

namespace DGVisionStudio.Infrastructure.Services;

public sealed class FileStoragePathService
{
    private readonly IWebHostEnvironment _environment;

    public FileStoragePathService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string ResolveDirectoryPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return GetWebRootPath();

        if (!TryResolvePath(folderPath, out var fullPath))
            throw new ArgumentException(
                "Folder path must stay within the configured web root.",
                nameof(folderPath));

        return fullPath;
    }

    public bool TryResolvePath(string relativePath, out string fullPath)
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
        var webRootPrefix = webRootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
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

    public string GetRelativeUrl(string fullPath)
    {
        var relativePath = Path.GetRelativePath(GetWebRootPath(), fullPath)
            .Replace('\\', '/');

        return "/" + relativePath;
    }

    private string GetWebRootPath()
    {
        var configuredPath = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        return Path.GetFullPath(configuredPath);
    }
}
