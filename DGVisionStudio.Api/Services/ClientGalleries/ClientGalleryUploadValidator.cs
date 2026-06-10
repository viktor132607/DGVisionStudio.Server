using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryUploadValidator
{
	private const long MaxImageUploadSizeBytes = 20 * 1024 * 1024;

	private static readonly Dictionary<string, string[]> AllowedImageContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
	{
		[".jpg"] = new[] { "image/jpeg" },
		[".jpeg"] = new[] { "image/jpeg" },
		[".png"] = new[] { "image/png" },
		[".webp"] = new[] { "image/webp" }
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
		if (!AllowedImageContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes))
			throw new ArgumentException("Only JPG, JPEG, PNG and WEBP files are allowed.");
		if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
			throw new ArgumentException("Invalid file MIME type.");

		await using var stream = file.OpenReadStream();
		var header = new byte[16];
		var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

		if (!HasValidImageSignature(extension, header, read))
			throw new ArgumentException("Invalid image file signature.");
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

		return false;
	}
}