using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace DGVisionStudio.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
	private readonly IWebHostEnvironment _environment;

	public LocalFileStorageService(IWebHostEnvironment environment)
	{
		_environment = environment;
	}

	public async Task<string> SaveFileAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		CancellationToken cancellationToken = default)
	{
		var webRootPath = GetWebRootPath();

		var safeFolderPath = folderPath
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar)
			.Trim(Path.DirectorySeparatorChar);

		var targetDirectory = Path.Combine(webRootPath, safeFolderPath);
		Directory.CreateDirectory(targetDirectory);

		var extension = Path.GetExtension(fileName);
		var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
		var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
		var fullPath = Path.Combine(targetDirectory, safeFileName);

		await using var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
		await fileStream.CopyToAsync(output, cancellationToken);

		return "/" + Path.Combine(safeFolderPath, safeFileName).Replace('\\', '/');
	}

	public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.CompletedTask;

		var fullPath = GetFullPath(relativePath);

		if (File.Exists(fullPath))
		{
			File.Delete(fullPath);
		}

		return Task.CompletedTask;
	}

	public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.FromResult<Stream?>(null);

		var fullPath = GetFullPath(relativePath);

		if (!File.Exists(fullPath))
			return Task.FromResult<Stream?>(null);

		Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
		return Task.FromResult<Stream?>(stream);
	}

	public Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.FromResult(false);

		var fullPath = GetFullPath(relativePath);
		return Task.FromResult(File.Exists(fullPath));
	}

	private string GetWebRootPath()
	{
		if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
			return _environment.WebRootPath;

		return Path.Combine(_environment.ContentRootPath, "wwwroot");
	}

	private string GetFullPath(string relativePath)
	{
		var webRootPath = GetWebRootPath();

		var normalizedRelativePath = relativePath
			.TrimStart('/', '\\')
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar);

		return Path.Combine(webRootPath, normalizedRelativePath);
	}
}