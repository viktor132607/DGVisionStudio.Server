using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace DGVisionStudio.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
	private readonly IWebHostEnvironment _environment;

	public FileStorageService(IWebHostEnvironment environment)
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
		var safeFolderPath = NormalizeFolderPath(folderPath);

		var targetDirectory = Path.Combine(webRootPath, safeFolderPath);
		Directory.CreateDirectory(targetDirectory);

		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		var generatedFileName = $"{Guid.NewGuid():N}{extension}";
		var fullPath = Path.Combine(targetDirectory, generatedFileName);

		await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
		await fileStream.CopyToAsync(output, cancellationToken);

		var relativePath = "/" + Path.Combine(safeFolderPath, generatedFileName).Replace('\\', '/');
		return relativePath;
	}

	public async Task<string> SaveImageAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		int maxWidth = 2400,
		int quality = 82,
		CancellationToken cancellationToken = default)
	{
		var webRootPath = GetWebRootPath();
		var safeFolderPath = NormalizeFolderPath(folderPath);

		var targetDirectory = Path.Combine(webRootPath, safeFolderPath);
		Directory.CreateDirectory(targetDirectory);

		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		var generatedFileName = $"{Guid.NewGuid():N}{extension}";
		var fullPath = Path.Combine(targetDirectory, generatedFileName);

		using var image = await ImageSharpImage.LoadAsync(fileStream, cancellationToken);

		if (maxWidth > 0 && image.Width > maxWidth)
		{
			image.Mutate(x => x.Resize(new ResizeOptions
			{
				Mode = ResizeMode.Max,
				Size = new Size(maxWidth, 0)
			}));
		}

		image.Metadata.ExifProfile = null;
		image.Metadata.IccProfile = null;
		image.Metadata.IptcProfile = null;
		image.Metadata.XmpProfile = null;

		await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

		if (extension is ".jpg" or ".jpeg")
		{
			await image.SaveAsJpegAsync(output, new JpegEncoder
			{
				Quality = quality
			}, cancellationToken);
		}
		else if (extension == ".png")
		{
			await image.SaveAsPngAsync(output, new PngEncoder
			{
				CompressionLevel = PngCompressionLevel.BestCompression
			}, cancellationToken);
		}
		else if (extension == ".webp")
		{
			await image.SaveAsWebpAsync(output, new WebpEncoder
			{
				Quality = quality
			}, cancellationToken);
		}
		else
		{
			throw new InvalidOperationException("Unsupported image format.");
		}

		var relativePath = "/" + Path.Combine(safeFolderPath, generatedFileName).Replace('\\', '/');
		return relativePath;
	}

	public Task DeleteFileAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
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

	public Task<Stream?> OpenReadAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return Task.FromResult<Stream?>(null);

		var fullPath = GetFullPath(relativePath);

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

		var fullPath = GetFullPath(relativePath);
		return Task.FromResult(File.Exists(fullPath));
	}

	private string GetWebRootPath()
	{
		if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
			return _environment.WebRootPath;

		return Path.Combine(_environment.ContentRootPath, "wwwroot");
	}

	private static string NormalizeFolderPath(string folderPath)
	{
		return folderPath
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar)
			.Trim(Path.DirectorySeparatorChar);
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