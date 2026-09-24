using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace DGVisionStudio.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly FileStorageFileService _files;
    private readonly FileStorageImageService _images;

    public FileStorageService(IWebHostEnvironment environment)
    {
        var paths = new FileStoragePathService(environment);
        _files = new FileStorageFileService(paths);
        _images = new FileStorageImageService(paths);
    }

    public FileStorageService(
        FileStorageFileService files,
        FileStorageImageService images)
    {
        _files = files;
        _images = images;
    }

    public Task<string> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        CancellationToken cancellationToken = default) =>
        _files.SaveFileAsync(
            fileStream,
            fileName,
            folderPath,
            cancellationToken);

    public Task<string> SaveImageAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        int maxWidth = 2400,
        int quality = 82,
        CancellationToken cancellationToken = default) =>
        _images.SaveImageAsync(
            fileStream,
            fileName,
            folderPath,
            maxWidth,
            quality,
            cancellationToken);

    public Task DeleteFileAsync(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        _files.DeleteFileAsync(relativePath, cancellationToken);

    public Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        _files.OpenReadAsync(relativePath, cancellationToken);

    public Task<bool> FileExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        _files.FileExistsAsync(relativePath, cancellationToken);
}
