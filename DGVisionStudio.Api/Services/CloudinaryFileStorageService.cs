using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DGVisionStudio.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Infrastructure.Services;

public class CloudinaryFileStorageService : IFileStorageService
{
	private const long CloudinaryMaxImageUploadSizeBytes = 10 * 1024 * 1024;
	private static readonly HttpClient HttpClient = new();

	private readonly Cloudinary _cloudinary;
	private readonly string _folder;

	public CloudinaryFileStorageService(IConfiguration configuration)
	{
		var cloudName = configuration["Cloudinary:CloudName"]?.Trim();
		var apiKey = configuration["Cloudinary:ApiKey"]?.Trim();
		var apiSecret = configuration["Cloudinary:ApiSecret"]?.Trim();

		_folder = (configuration["Cloudinary:Folder"] ?? "dgvisionstudio/portfolio")
			.Trim()
			.Trim('/');

		if (string.IsNullOrWhiteSpace(cloudName))
			throw new InvalidOperationException("Cloudinary:CloudName is missing.");

		if (string.IsNullOrWhiteSpace(apiKey))
			throw new InvalidOperationException("Cloudinary:ApiKey is missing.");

		if (string.IsNullOrWhiteSpace(apiSecret))
			throw new InvalidOperationException("Cloudinary:ApiSecret is missing.");

		var account = new Account(cloudName, apiKey, apiSecret);

		_cloudinary = new Cloudinary(account)
		{
			Api =
			{
				Secure = true
			}
		};
	}

	public Task<string> SaveFileAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		CancellationToken cancellationToken = default)
	{
		return SaveImageAsync(fileStream, fileName, folderPath, cancellationToken: cancellationToken);
	}

	public async Task<string> SaveImageAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		int maxWidth = 2400,
		int quality = 82,
		CancellationToken cancellationToken = default)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();

		if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
			throw new InvalidOperationException("Unsupported image format.");

		if (fileStream.CanSeek && fileStream.Length > CloudinaryMaxImageUploadSizeBytes)
			return BuildSkippedCloudinaryPath(folderPath, fileName);

		var publicId = BuildPublicId(folderPath, Path.GetFileNameWithoutExtension(fileName));

		var uploadParams = new ImageUploadParams
		{
			File = new FileDescription(fileName, fileStream),
			PublicId = publicId,
			Overwrite = false,
			UseFilename = false,
			UniqueFilename = true,
			Transformation = new Transformation()
				.Width(maxWidth)
				.Crop("limit")
				.Quality(quality)
		};

		var result = await _cloudinary.UploadAsync(uploadParams);

		if (result.Error != null)
			throw new InvalidOperationException($"Cloudinary upload failed. Error: {result.Error.Message}");

		if (string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()))
			throw new InvalidOperationException("Cloudinary upload failed. SecureUrl is empty.");

		return result.SecureUrl.ToString();
	}

	public async Task DeleteFileAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		var publicId = ExtractPublicId(relativePath);

		if (string.IsNullOrWhiteSpace(publicId))
			return;

		var deleteParams = new DeletionParams(publicId)
		{
			ResourceType = ResourceType.Image
		};

		var result = await _cloudinary.DestroyAsync(deleteParams);

		if (result.Error != null)
			throw new InvalidOperationException($"Cloudinary delete failed. Error: {result.Error.Message}");
	}

	public async Task<Stream?> OpenReadAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return null;

		try
		{
			var response = await HttpClient.GetAsync(relativePath, cancellationToken);

			if (!response.IsSuccessStatusCode)
				return null;

			var memoryStream = new MemoryStream();
			await response.Content.CopyToAsync(memoryStream, cancellationToken);
			memoryStream.Position = 0;

			return memoryStream;
		}
		catch
		{
			return null;
		}
	}

	public async Task<bool> FileExistsAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		var publicId = ExtractPublicId(relativePath);

		if (string.IsNullOrWhiteSpace(publicId))
			return false;

		try
		{
			var result = await _cloudinary.GetResourceAsync(new GetResourceParams(publicId));
			return result != null && result.Error == null;
		}
		catch
		{
			return false;
		}
	}

	private string BuildPublicId(string folderPath, string fileNameWithoutExtension)
	{
		var safeFolder = NormalizeCloudinaryFolder(folderPath);
		var safeName = string.IsNullOrWhiteSpace(fileNameWithoutExtension)
			? Guid.NewGuid().ToString("N")
			: SanitizePublicId(fileNameWithoutExtension);

		var uniqueName = $"{safeName}-{Guid.NewGuid():N}";

		return string.IsNullOrWhiteSpace(safeFolder)
			? $"{_folder}/{uniqueName}"
			: $"{_folder}/{safeFolder}/{uniqueName}";
	}

	private static string BuildSkippedCloudinaryPath(string folderPath, string fileName)
	{
		var safeFolder = NormalizeCloudinaryFolder(folderPath);
		var safeFileName = string.IsNullOrWhiteSpace(fileName)
			? $"{Guid.NewGuid():N}.jpg"
			: Path.GetFileName(fileName);

		return string.IsNullOrWhiteSpace(safeFolder)
			? safeFileName
			: $"{safeFolder}/{safeFileName}";
	}

	private static string NormalizeCloudinaryFolder(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		var trimmed = value.Trim().Replace("\\", "/");

		if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
			trimmed = uri.AbsolutePath;

		return trimmed.Trim('/').Replace("uploads/", "");
	}

	private static string SanitizePublicId(string value)
	{
		var safe = value.Trim().ToLowerInvariant();

		foreach (var invalidChar in Path.GetInvalidFileNameChars())
		{
			safe = safe.Replace(invalidChar, '-');
		}

		safe = safe
			.Replace(" ", "-")
			.Replace("_", "-")
			.Replace(".", "-");

		while (safe.Contains("--", StringComparison.Ordinal))
		{
			safe = safe.Replace("--", "-");
		}

		return string.IsNullOrWhiteSpace(safe.Trim('-'))
			? Guid.NewGuid().ToString("N")
			: safe.Trim('-');
	}

	private string ExtractPublicId(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		var input = value.Trim();

		if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
			return StripKnownExtension(input.Trim('/'));

		var path = uri.AbsolutePath.Trim('/');
		var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

		var uploadIndex = parts.FindIndex(x => x.Equals("upload", StringComparison.OrdinalIgnoreCase));
		if (uploadIndex >= 0)
			parts = parts.Skip(uploadIndex + 1).ToList();

		if (parts.Count > 0 && parts[0].StartsWith("v", StringComparison.OrdinalIgnoreCase) && parts[0].Skip(1).All(char.IsDigit))
			parts.RemoveAt(0);

		return StripKnownExtension(string.Join("/", parts));
	}

	private static string StripKnownExtension(string value)
	{
		var extension = Path.GetExtension(value);

		return extension.ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".pdf" => value[..^extension.Length],
			_ => value
		};
	}
}
