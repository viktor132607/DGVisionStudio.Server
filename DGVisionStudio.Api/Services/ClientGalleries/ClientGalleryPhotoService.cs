using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryPhotoService : IClientGalleryPhotoService
{
	private readonly AppDbContext _dbContext;
	private readonly IFileStorageService _fileStorageService;
	private readonly ClientGalleryMapper _mapper;
	private readonly ClientGalleryUploadValidator _uploadValidator;
	private readonly ClientGalleryNamingService _namingService;
	private readonly ILogger<ClientGalleryPhotoService> _logger;

	public ClientGalleryPhotoService(
		AppDbContext dbContext,
		IFileStorageService fileStorageService,
		ClientGalleryMapper mapper,
		ClientGalleryUploadValidator uploadValidator,
		ClientGalleryNamingService namingService,
		ILogger<ClientGalleryPhotoService> logger)
	{
		_dbContext = dbContext;
		_fileStorageService = fileStorageService;
		_mapper = mapper;
		_uploadValidator = uploadValidator;
		_namingService = namingService;
		_logger = logger;
	}

	public async Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(int galleryId, int photoId, string userId, bool isAdmin)
	{
		var now = DateTime.UtcNow;

		var photo = await _dbContext.PortfolioImages
			.AsNoTracking()
			.Include(x => x.PortfolioAlbum!)
				.ThenInclude(x => x.UserAccesses)
			.FirstOrDefaultAsync(x =>
				x.Id == photoId &&
				x.PortfolioAlbumId == galleryId &&
				!x.IsDeleted);

		if (photo == null || photo.PortfolioAlbum == null || photo.PortfolioAlbum.IsDeleted)
			return null;

		var album = photo.PortfolioAlbum;

		var allowed = false;

		if (isAdmin)
		{
			allowed = true;
		}
		else if (album.GalleryType == GalleryType.ClientPrintUpload && album.IsUserUploaded && album.OwnerUserId == userId && !_mapper.IsUserGalleryExpired(album, now))
		{
			allowed = true;
		}
		else
		{
			var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
			allowed = access != null && _mapper.IsDownloadActive(access, now);
		}

		if (!allowed)
		{
			_logger.LogWarning(
				"Photo download denied. GalleryId: {GalleryId}, PhotoId: {PhotoId}, UserId: {UserId}, IsAdmin: {IsAdmin}, GalleryType: {GalleryType}, IsUserUploaded: {IsUserUploaded}, OwnerUserId: {OwnerUserId}",
				galleryId,
				photoId,
				userId,
				isAdmin,
				album.GalleryType,
				album.IsUserUploaded,
				album.OwnerUserId);

			return null;
		}

		if (string.IsNullOrWhiteSpace(photo.ImageUrl))
			return null;

		var stream = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
		if (stream == null)
			return null;

		var extension = Path.GetExtension(photo.ImageUrl).ToLowerInvariant();
		var contentType = GetContentType(extension);
		var fileName = $"{_namingService.Slugify(album.Title)}-{photo.Id}{extension}";

		_logger.LogInformation(
			"Photo download opened. GalleryId: {GalleryId}, PhotoId: {PhotoId}, UserId: {UserId}, IsAdmin: {IsAdmin}, FileName: {FileName}",
			galleryId,
			photoId,
			userId,
			isAdmin,
			fileName);

		return (stream, contentType, fileName);
	}

	public async Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file)
	{
		await _uploadValidator.ValidateUploadedImageAsync(file);

		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);
		if (album == null) return null;

		var safeExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
		var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);

		await using var stream = file.OpenReadStream();

		var savedPath = await _fileStorageService.SaveImageAsync(
			stream,
			safeFileName,
			Path.Combine("uploads", "portfolio"),
			maxWidth: 2400,
			quality: 82,
			CancellationToken.None);

		var nextDisplayOrder = await _dbContext.PortfolioImages
			.Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var hasActiveImages = await _dbContext.PortfolioImages
			.AnyAsync(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted);

		var photo = new PortfolioImage
		{
			PortfolioAlbumId = galleryId,
			ImageUrl = savedPath,
			ThumbnailUrl = savedPath,
			AltText = string.IsNullOrWhiteSpace(originalFileNameWithoutExtension) ? null : originalFileNameWithoutExtension.Trim(),
			Caption = null,
			DisplayOrder = nextDisplayOrder + 1,
			IsCover = !hasActiveImages,
			IsPublished = true,
			IsDeleted = false,
			DeletedAtUtc = null
		};

		_dbContext.PortfolioImages.Add(photo);

		if (string.IsNullOrWhiteSpace(album.CoverImageUrl) || !hasActiveImages)
		{
			album.CoverImageUrl = savedPath;
			photo.IsCover = true;
		}

		if (album.GalleryType == GalleryType.Photoshoot &&
			album.UserGalleryStatus != UserClientGalleryStatus.PhotoshootInProgress &&
			album.UserGalleryStatus != UserClientGalleryStatus.PhotoshootReadyForPickup &&
			album.UserGalleryStatus != UserClientGalleryStatus.PhotoshootCancelled)
		{
			album.UserGalleryStatus = UserClientGalleryStatus.PhotoshootUploaded;
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin/service uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, SavedPath: {SavedPath}",
			galleryId,
			photo.Id,
			file.FileName,
			file.Length,
			file.ContentType,
			savedPath);

		return _mapper.MapPhotoDto(photo, true, galleryId);
	}

	public async Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var photo = await _dbContext.PortfolioImages
			.Include(x => x.PortfolioAlbum!)
				.ThenInclude(x => x.Images)
			.FirstOrDefaultAsync(x =>
				x.Id == photoId &&
				x.PortfolioAlbumId == galleryId &&
				!x.IsDeleted &&
				!x.PortfolioAlbum!.IsDeleted);

		if (photo == null) return null;

		photo.AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim();
		photo.Caption = string.IsNullOrWhiteSpace(request.Caption)
			? (string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim())
			: request.Caption.Trim();
		photo.DisplayOrder = request.DisplayOrder ?? photo.DisplayOrder;

		if (request.IsPublished.HasValue) photo.IsPublished = request.IsPublished.Value;

		if (request.IsCover == true && photo.PortfolioAlbum != null)
		{
			foreach (var image in photo.PortfolioAlbum.Images.Where(x => !x.IsDeleted))
			{
				image.IsCover = image.Id == photo.Id;
			}

			photo.PortfolioAlbum.CoverImageUrl = photo.ThumbnailUrl ?? photo.ImageUrl;
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Gallery photo updated. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}",
			galleryId,
			photoId,
			request.IsPublished,
			request.IsCover);

		return _mapper.MapPhotoDto(photo, true, galleryId);
	}

	public async Task<bool> DeletePhotoAsync(int galleryId, int photoId)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null) return false;

		var photo = album.Images.FirstOrDefault(x => x.Id == photoId && !x.IsDeleted);
		if (photo == null) return false;

		var now = DateTime.UtcNow;
		var imageUrl = photo.ImageUrl;
		var thumbnailUrl = photo.ThumbnailUrl;

		photo.IsDeleted = true;
		photo.DeletedAtUtc = now;
		photo.IsPublished = false;
		photo.IsCover = false;

		if (string.Equals(album.CoverImageUrl, photo.ThumbnailUrl, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(album.CoverImageUrl, photo.ImageUrl, StringComparison.OrdinalIgnoreCase))
		{
			var fallback = album.Images
				.Where(x => x.Id != photoId && !x.IsDeleted)
				.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Id)
				.FirstOrDefault();

			album.CoverImageUrl = fallback?.ThumbnailUrl ?? fallback?.ImageUrl;

			foreach (var image in album.Images)
			{
				image.IsCover = fallback != null && image.Id == fallback.Id;
			}
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogWarning(
			"Gallery photo soft deleted. GalleryId: {GalleryId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}, ThumbnailUrl: {ThumbnailUrl}",
			galleryId,
			photoId,
			imageUrl,
			thumbnailUrl);

		return true;
	}

	public async Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null) return false;

		var normalizedCoverImageUrl = NormalizeStoredImagePath(coverImageUrl);

		var matchingPhoto = album.Images.FirstOrDefault(x =>
			!x.IsDeleted &&
			(string.Equals(NormalizeStoredImagePath(x.ThumbnailUrl), normalizedCoverImageUrl, StringComparison.OrdinalIgnoreCase) ||
			 string.Equals(NormalizeStoredImagePath(x.ImageUrl), normalizedCoverImageUrl, StringComparison.OrdinalIgnoreCase)));

		if (matchingPhoto == null) return false;

		foreach (var image in album.Images.Where(x => !x.IsDeleted))
		{
			image.IsCover = image.Id == matchingPhoto.Id;
		}

		album.CoverImageUrl = matchingPhoto.ThumbnailUrl ?? matchingPhoto.ImageUrl;

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Gallery cover image changed. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, NormalizedCoverImageUrl: {NormalizedCoverImageUrl}",
			galleryId,
			coverImageUrl,
			normalizedCoverImageUrl);

		return true;
	}

	public async Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var albumExists = await _dbContext.PortfolioAlbums.AnyAsync(x => x.Id == galleryId && !x.IsDeleted);
		if (!albumExists) return false;

		var photos = await _dbContext.PortfolioImages
			.Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
			.ToListAsync();

		if (photos.Count == 0) return false;

		var photoMap = photos.ToDictionary(x => x.Id);
		var order = 1;

		foreach (var photoId in orderedPhotoIds)
		{
			if (photoMap.TryGetValue(photoId, out var photo))
			{
				photo.DisplayOrder = order++;
			}
		}

		foreach (var remaining in photos
			.Where(x => !orderedPhotoIds.Contains(x.Id))
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id))
		{
			remaining.DisplayOrder = order++;
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Gallery photos reordered. GalleryId: {GalleryId}, PhotoCount: {PhotoCount}",
			galleryId,
			orderedPhotoIds.Count);

		return true;
	}

	private static string? NormalizeStoredImagePath(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var trimmed = value.Trim().Replace("\\", "/");

		if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
			trimmed = uri.AbsolutePath;

		return trimmed.TrimStart('/');
	}

	private static string GetContentType(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".webp" => "image/webp",
			_ => "application/octet-stream"
		};
	}
}
