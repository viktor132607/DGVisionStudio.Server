namespace DGVisionStudio.Infrastructure.Services;

public sealed class FileStorageFileService
{
    private readonly FileStoragePathService _paths;

    public FileStorageFileService(FileStoragePathService paths)
    {
        _paths = paths;
    }

    public async Task<string> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        var targetDirectory = _paths.ResolveDirectoryPath(folderPath);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, generatedFileName);

        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        await fileStream.CopyToAsync(output, cancellationToken);

        return _paths.GetRelativeUrl(fullPath);
    }

    public Task DeleteFileAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!_paths.TryResolvePath(relativePath, out var fullPath))
            return Task.CompletedTask;

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!_paths.TryResolvePath(relativePath, out var fullPath) || !File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> FileExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _paths.TryResolvePath(relativePath, out var fullPath) &&
            File.Exists(fullPath));
    }
}
