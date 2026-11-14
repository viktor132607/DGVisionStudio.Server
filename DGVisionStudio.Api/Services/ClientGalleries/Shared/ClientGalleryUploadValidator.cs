using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryUploadValidator
{
	private const long MaxImageUploadSizeBytes = 20 * 1024 * 1024;

	private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
	};

	private static readonly Dictionary<string, string[]> AllowedImageContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
	{
		[".jpg"] = new[] { "image/jpeg", "image/jpg", "image/pjpeg", "application/octet-stream" },
		[".jpeg"] = new[] { "image/jpeg", "image/jpg", "image/pjpeg", "application/octet-stream" },
		[".png"] = new[] { "image/png", "application/octet-stream" },
		[".webp"] = new[] { "image/webp", "application/octet-stream" },
		[".bmp"] = new[] { "image/bmp", "image/x-bmp", "image/x-ms-bmp", "application/octet-stream" },
		[".gif"] = new[] { "image/gif", "application/octet-stream" }
	};

	private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".ps1", ".vbs", ".js", ".jar", ".msi", ".sh", ".php", ".aspx", ".html", ".htm", ".svg"
	};

	public async Task ValidateUploadedImageAsync(IFormFile file)
	{
		if (file == null || file.Length == 0) throw new ArgumentException("File is required.");
		if (file.Length > MaxImageUploadSizeBytes) throw new ArgumentException("File size cannot exceed 20 MB.");

		var originalFileName = Path.GetFileName(file.FileName);
		if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentException("Invalid file name.");

		var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("File extension is required.");
		if (DangerousExtensions.Contains(extension)) throw new ArgumentException("File type is not allowed.");
		if (!AllowedImageExtensions.Contains(extension))
			throw new ArgumentException("Only JPG, JPEG, PNG, WEBP, BMP and GIF files are allowed.");

		if ((extension is ".bmp" or ".gif") && file.Length > 10 * 1024 * 1024)
			throw new ArgumentException("BMP and GIF files cannot exceed 10 MB; the original format is preserved.");

		await using var stream = file.OpenReadStream();
		var header = new byte[16];
		var read = await stream.ReadAtLeastAsync(header.AsMemory(), header.Length, throwOnEndOfStream: false);

		if (!HasValidImageSignature(extension, header, read))
			throw new ArgumentException("Invalid image file signature.");

		if (!HasAllowedContentType(extension, file.ContentType))
			throw new ArgumentException("Invalid file MIME type.");
		// A plausible header alone does not prove that a BMP/GIF is decodable.
		if (extension is ".bmp" or ".gif")
		{
			try
			{
				await using var imageStream = file.OpenReadStream();
				using var image = await Image.LoadAsync(imageStream);
			}
			catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException or NotSupportedException)
			{
				throw new ArgumentException("Invalid or damaged image file.", ex);
			}
		}
	}

	private static bool HasAllowedContentType(string extension, string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
			return true;

		return AllowedImageContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes) &&
			allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
	}

	private static bool HasValidImageSignature(string extension, byte[] header, int bytesRead)
	{
		if ((extension == ".jpg" || extension == ".jpeg") && bytesRead >= 3)
			return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

		if (extension == ".png" && bytesRead >= 8)
			return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
			       header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

		if (extension == ".webp" && bytesRead >= 12)
			return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
			       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

		if (extension == ".bmp" && bytesRead >= 2)
			return header[0] == 0x42 && header[1] == 0x4D;

		if (extension == ".gif" && bytesRead >= 6)
			return header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 &&
				header[3] == 0x38 && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61;

		return false;
	}
}
