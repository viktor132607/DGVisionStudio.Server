namespace DGVisionStudio.Api.Services;

public sealed class DatabaseBackupTempFileManager
{
    public string CreateTemporaryPath(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string normalizedExtension = extension.Trim().TrimStart('.');
        string fileName = $"dgvisionstudio-db-{Guid.NewGuid():N}.{normalizedExtension}";
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    public async Task<string> CopyToTemporaryFileAsync(
        Stream source,
        string extension,
        int bufferSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        string path = CreateTemporaryPath(extension);

        try
        {
            await using FileStream destination = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await source.CopyToAsync(destination, bufferSize, cancellationToken);
            return path;
        }
        catch
        {
            Delete(path);
            throw;
        }
    }

    public void Delete(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Temporary-file cleanup must never hide the original backup/restore result.
        }
    }

    public void DeleteAll(params string?[] filePaths)
    {
        foreach (string? filePath in filePaths)
            Delete(filePath);
    }
}
