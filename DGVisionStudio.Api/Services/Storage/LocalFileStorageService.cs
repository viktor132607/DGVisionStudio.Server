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
		var safeFolderPath = NormalizeRelativePath(folderPath);
		var targetDirectory = ResolveWithinWebRoot(safeFolderPath);
		Directory.CreateDirectory(targetDirectory);

		var extension = Path.GetExtension(fileName);
		var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
		var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
		var fullPath = ResolveWithinWebRoot(Path.Combine(safeFolderPath, safeFileName));

		await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
		await fileStream.CopyToAsync(output, cancellationToken);

		return "/" + Path.Combine(safeFolderPath, safeFileName).Replace('\\', '/');
	}

	public Task<string> SaveImageAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		int maxWidth = 2400,
		int quality = 82,
		CancellationToken cancellationToken = default)
	{
		return SaveFileAsync(fileStream, fileName, folderPath, cancellationToken);
	}

	public Task DeleteFileAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.CompletedTask;

		var fullPath = ResolveWithinWebRoot(NormalizeRelativePath(relativePath));

		if (File.Exists(fullPath))
		{
			File.Delete(fullPath);
		}

		return Task.CompletedTask;
	}

	public Task<Stream?> OpenReadAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.FromResult<Stream?>(null);

		var fullPath = ResolveWithinWebRoot(NormalizeRelativePath(relativePath));

		if (!File.Exists(fullPath))
			return Task.FromResult<Stream?>(null);

		Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
		return Task.FromResult<Stream?>(stream);
	}

	public Task<bool> FileExistsAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.FromResult(false);

		var fullPath = ResolveWithinWebRoot(NormalizeRelativePath(relativePath));
		return Task.FromResult(File.Exists(fullPath));
	}

	private string GetWebRootPath()
	{
		if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
			return _environment.WebRootPath;

		return Path.Combine(_environment.ContentRootPath, "wwwroot");
	}

	private static string NormalizeRelativePath(string value) =>
		value
			.TrimStart('/', '\\')
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar);

	private string ResolveWithinWebRoot(string relativePath)
	{
		var webRootPath = Path.GetFullPath(GetWebRootPath());
		var fullPath = Path.GetFullPath(Path.Combine(webRootPath, relativePath));
		var rootPrefix = webRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

		if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(fullPath, webRootPath, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Storage path must stay within the web root.");
		}

		return fullPath;
	}
}
