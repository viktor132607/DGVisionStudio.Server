using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DGVisionStudio.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace DGVisionStudio.Infrastructure.Services;

public class R2FileStorageService : IFileStorageService
{
	private readonly IAmazonS3 _s3Client;
	private readonly string _bucketName;
	private readonly string _publicBaseUrl;

	public R2FileStorageService(IConfiguration configuration)
	{
		var accessKeyId = configuration["R2:AccessKeyId"];
		var secretAccessKey = configuration["R2:SecretAccessKey"];
		var serviceUrl = configuration["R2:ServiceUrl"];

		_bucketName = configuration["R2:BucketName"] ?? "";
		_publicBaseUrl = (configuration["R2:PublicBaseUrl"] ?? "").TrimEnd('/');

		if (string.IsNullOrWhiteSpace(accessKeyId))
			throw new InvalidOperationException("R2:AccessKeyId is missing.");

		if (string.IsNullOrWhiteSpace(secretAccessKey))
			throw new InvalidOperationException("R2:SecretAccessKey is missing.");

		if (string.IsNullOrWhiteSpace(serviceUrl))
			throw new InvalidOperationException("R2:ServiceUrl is missing.");

		if (string.IsNullOrWhiteSpace(_bucketName))
			throw new InvalidOperationException("R2:BucketName is missing.");

		if (string.IsNullOrWhiteSpace(_publicBaseUrl))
			throw new InvalidOperationException("R2:PublicBaseUrl is missing.");

		var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

		var config = new AmazonS3Config
		{
			ServiceURL = serviceUrl,
			ForcePathStyle = true,
			AuthenticationRegion = "us-east-1",
			RegionEndpoint = RegionEndpoint.USEast1
		};

		_s3Client = new AmazonS3Client(credentials, config);
	}

	public async Task<string> SaveFileAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		CancellationToken cancellationToken = default)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		var generatedFileName = $"{Guid.NewGuid():N}{extension}";
		var key = BuildObjectKey(folderPath, generatedFileName);

		await PutObjectAsync(fileStream, key, GetContentType(extension), cancellationToken);

		return BuildPublicUrl(key);
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

		var generatedFileName = $"{Guid.NewGuid():N}{extension}";
		var key = BuildObjectKey(folderPath, generatedFileName);

		await using var output = new MemoryStream();

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

		output.Position = 0;

		await PutObjectAsync(output, key, GetContentType(extension), cancellationToken);

		return BuildPublicUrl(key);
	}

	public async Task DeleteFileAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		var key = NormalizeObjectKey(relativePath);

		if (string.IsNullOrWhiteSpace(key))
			return;

		await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
		{
			BucketName = _bucketName,
			Key = key
		}, cancellationToken);
	}

	public async Task<Stream?> OpenReadAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		var key = NormalizeObjectKey(relativePath);

		if (string.IsNullOrWhiteSpace(key))
			return null;

		try
		{
			using var response = await _s3Client.GetObjectAsync(new GetObjectRequest
			{
				BucketName = _bucketName,
				Key = key
			}, cancellationToken);

			var memoryStream = new MemoryStream();
			await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
			memoryStream.Position = 0;

			return memoryStream;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	public async Task<bool> FileExistsAsync(
		string relativePath,
		CancellationToken cancellationToken = default)
	{
		var key = NormalizeObjectKey(relativePath);

		if (string.IsNullOrWhiteSpace(key))
			return false;

		try
		{
			await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
			{
				BucketName = _bucketName,
				Key = key
			}, cancellationToken);

			return true;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	private async Task PutObjectAsync(
		Stream stream,
		string key,
		string contentType,
		CancellationToken cancellationToken)
	{
		try
		{
			await _s3Client.PutObjectAsync(new PutObjectRequest
			{
				BucketName = _bucketName,
				Key = key,
				InputStream = stream,
				ContentType = contentType
			}, cancellationToken);
		}
		catch (AmazonS3Exception ex)
		{
			throw new InvalidOperationException(
				$"R2 upload failed. " +
				$"StatusCode: {(int)ex.StatusCode} {ex.StatusCode}. " +
				$"ErrorCode: {ex.ErrorCode}. " +
				$"AmazonMessage: {ex.Message}. " +
				$"Bucket: {_bucketName}. " +
				$"Key: {key}. " +
				$"ContentType: {contentType}.",
				ex);
		}
	}

	private string BuildPublicUrl(string key)
	{
		return $"{_publicBaseUrl}/{key.TrimStart('/')}";
	}

	private static string BuildObjectKey(string folderPath, string fileName)
	{
		var safeFolder = NormalizeObjectKey(folderPath);
		var safeFileName = Path.GetFileName(fileName);

		return string.IsNullOrWhiteSpace(safeFolder)
			? safeFileName
			: $"{safeFolder}/{safeFileName}";
	}

	private static string NormalizeObjectKey(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		var trimmed = value.Trim().Replace("\\", "/");

		if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
			trimmed = uri.AbsolutePath;

		return trimmed.TrimStart('/').TrimEnd('/');
	}

	private static string GetContentType(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".webp" => "image/webp",
			".gif" => "image/gif",
			".pdf" => "application/pdf",
			_ => "application/octet-stream"
		};
	}
}