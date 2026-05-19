using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryUserService : IClientGalleryUserService
{
	private const int MaxUserUploadedGalleries = 10;
	private const int UserUploadedGalleryLifetimeDays = 7;

	private readonly AppDbContext _dbContext;
	private readonly IFileStorageService _fileStorageService;
	private readonly ClientGalleryMapper _mapper;
	private readonly ClientGalleryUploadValidator _uploadValidator;
	private readonly ClientGalleryNamingService _namingService;
	private readonly ILogger<ClientGalleryUserService> _logger;

	public ClientGalleryUserService(
		AppDbContext dbContext,
		IFileStorageService fileStorageService,
		ClientGalleryMapper mapper,
		ClientGalleryUploadValidator uploadValidator,
		ClientGalleryNamingService namingService,
		ILogger<ClientGalleryUserService> logger)
	{
		_dbContext = dbContext;
		_fileStorageService = fileStorageService;
		_mapper = mapper;
		_uploadValidator = uploadValidator;
		_namingService = namingService;
		_logger = logger;
	}

	public async Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId)
	{
		var now = DateTime.UtcNow;

		var accessAlbums = await _dbContext.UserAlbumAccesses
			.AsNoTracking()
			.Include(x => x.PortfolioAlbum)
				.ThenInclude(x => x.PortfolioCategory)
			.Include(x => x.PortfolioAlbum)
				.ThenInclude(x => x.OwnerUser)
			.Where(x =>
				x.UserId == userId &&
				x.PortfolioAlbum.AllowClientAccess &&
				!x.PortfolioAlbum.IsDeleted)
			.Select(x => new
			{
				Album = x.PortfolioAlbum,
				Access = x
			})
			.ToListAsync();

		var ownedAlbums = await _dbContext.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.PortfolioCategory)
			.Include(x => x.OwnerUser)
			.Where(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				x.OwnerUserId == userId &&
				x.AllowClientAccess &&
				!x.IsDeleted)
			.ToListAsync();

		var result = new List<MyClientGalleryDto>();

		foreach (var item in accessAlbums)
		{
			result.Add(_mapper.MapGalleryDto(item.Album, now, item.Access));
		}

		foreach (var album in ownedAlbums)
		{
			if (result.Any(x => x.Id == album.Id))
				continue;

			result.Add(_mapper.MapGalleryDto(album, now, null, isOwner: true));
		}

		return result
			.OrderByDescending(x => x.CreatedSortDate())
			.ThenByDescending(x => x.Id)
			.ToList();
	}

	public async Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId)
	{
		var now = DateTime.UtcNow;

		var album = await _dbContext.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.PortfolioCategory)
			.Include(x => x.OwnerUser)
			.Include(x => x.Images)
			.Include(x => x.UserAccesses)
				.ThenInclude(x => x.User)
			.FirstOrDefaultAsync(x =>
				x.Id == galleryId &&
				x.AllowClientAccess &&
				!x.IsDeleted);

		if (album == null) return null;

		var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
		var isOwner = album.GalleryType == GalleryType.ClientPrintUpload && album.IsUserUploaded && album.OwnerUserId == userId;

		if (!isOwner && access == null) return null;

		var canDownload = isOwner
			? !_mapper.IsUserGalleryExpired(album, now)
			: access != null && _mapper.IsDownloadActive(access, now);

		return _mapper.MapGalleryDetailsDto(album, now, access, canDownload, isOwner);
	}

	public async Task<int?> CreateUserGalleryAsync(string userId, CreateUserClientGalleryRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var now = DateTime.UtcNow;

		var activeUserGalleryCount = await _dbContext.PortfolioAlbums
			.CountAsync(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				x.OwnerUserId == userId &&
				x.AllowClientAccess &&
				!x.IsDeleted &&
				x.ExpiresAtUtc != null &&
				x.ExpiresAtUtc > now);

		if (activeUserGalleryCount >= MaxUserUploadedGalleries)
		{
			_logger.LogWarning(
				"User client gallery creation rejected because limit was reached. UserId: {UserId}, ActiveGalleryCount: {ActiveGalleryCount}, Limit: {Limit}",
				userId,
				activeUserGalleryCount,
				MaxUserUploadedGalleries);

			return null;
		}

		var title = request.Title.Trim();
		if (string.IsNullOrWhiteSpace(title)) return null;

		var categoryId = await _namingService.EnsureClientAlbumsCategoryAsync();

		var maxDisplayOrder = await _dbContext.PortfolioAlbums
			.Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var album = new PortfolioAlbum
		{
			PortfolioCategoryId = categoryId,
			Slug = await _namingService.BuildUniqueSlugAsync(title),
			Title = title,
			TitleEn = null,
			Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
			CoverImageUrl = null,
			DisplayOrder = maxDisplayOrder + 1,
			IsPublished = false,
			AllowClientAccess = true,
			GalleryType = GalleryType.ClientPrintUpload,
			IsUserUploaded = true,
			OwnerUserId = userId,
			ExpiresAtUtc = now.AddDays(UserUploadedGalleryLifetimeDays),
			UserGalleryStatus = UserClientGalleryStatus.Pending,
			IsDeleted = false,
			DeletedAtUtc = null,
			CreatedAtUtc = now
		};

		_dbContext.PortfolioAlbums.Add(album);
		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"User client gallery created. GalleryId: {GalleryId}, OwnerUserId: {OwnerUserId}, Title: {Title}, GalleryType: {GalleryType}, ExpiresAtUtc: {ExpiresAtUtc}, Status: {Status}",
			album.Id,
			album.OwnerUserId,
			album.Title,
			album.GalleryType,
			album.ExpiresAtUtc,
			album.UserGalleryStatus);

		return album.Id;
	}

	public async Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(int galleryId, string userId, IFormFile file)
	{
		await _uploadValidator.ValidateUploadedImageAsync(file);

		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var now = DateTime.UtcNow;

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x =>
				x.Id == galleryId &&
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				x.OwnerUserId == userId &&
				x.AllowClientAccess &&
				!x.IsDeleted);

		if (album == null) return null;

		if (_mapper.IsUserGalleryExpired(album, now))
		{
			_logger.LogWarning(
				"User gallery photo upload rejected because gallery expired. GalleryId: {GalleryId}, OwnerUserId: {OwnerUserId}, ExpiresAtUtc: {ExpiresAtUtc}",
				galleryId,
				userId,
				album.ExpiresAtUtc);

			return null;
		}

		var safeExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
		var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);

		await using var stream = file.OpenReadStream();

		var savedPath = await _fileStorageService.SaveImageAsync(
			stream,
			safeFileName,
			Path.Combine("uploads", "client-galleries", "originals"),
			maxWidth: 2400,
			quality: 82,
			CancellationToken.None);

		var nextDisplayOrder = album.Images
			.Where(x => !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.Max() ?? 0;

		var photo = new PortfolioImage
		{
			PortfolioAlbumId = galleryId,
			ImageUrl = savedPath,
			ThumbnailUrl = savedPath,
			AltText = string.IsNullOrWhiteSpace(originalFileNameWithoutExtension) ? null : originalFileNameWithoutExtension.Trim(),
			Caption = null,
			DisplayOrder = nextDisplayOrder + 1,
			IsCover = !album.Images.Any(x => !x.IsDeleted),
			IsPublished = true,
			IsDeleted = false,
			DeletedAtUtc = null,
			CreatedAtUtc = now
		};

		_dbContext.PortfolioImages.Add(photo);

		if (string.IsNullOrWhiteSpace(album.CoverImageUrl))
		{
			album.CoverImageUrl = savedPath;
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"User uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, OwnerUserId: {OwnerUserId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, SavedPath: {SavedPath}",
			galleryId,
			photo.Id,
			userId,
			file.FileName,
			file.Length,
			file.ContentType,
			savedPath);

		return _mapper.MapPhotoDto(photo, true, galleryId);
	}

	public async Task<bool> DeleteUserGalleryAsync(int galleryId, string userId)
	{
		var now = DateTime.UtcNow;

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x =>
				x.Id == galleryId &&
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				x.OwnerUserId == userId &&
				x.AllowClientAccess &&
				!x.IsDeleted);

		if (album == null)
			return false;

		foreach (var photo in album.Images.Where(x => !x.IsDeleted).ToList())
		{
			if (!string.IsNullOrWhiteSpace(photo.ImageUrl))
			{
				try
				{
					await _fileStorageService.DeleteFileAsync(photo.ImageUrl);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to delete uploaded user gallery photo from storage. GalleryId: {GalleryId}, PhotoId: {PhotoId}, ImageUrl: {ImageUrl}", galleryId, photo.Id, photo.ImageUrl);
				}
			}

			photo.IsDeleted = true;
			photo.DeletedAtUtc = now;
			photo.IsPublished = false;
		}

		album.IsDeleted = true;
		album.DeletedAtUtc = now;
		album.IsPublished = false;
		album.AllowClientAccess = false;
		album.CoverImageUrl = null;

		await _dbContext.SaveChangesAsync();

		_logger.LogInformation("User uploaded gallery deleted by owner. GalleryId: {GalleryId}, OwnerUserId: {OwnerUserId}", galleryId, userId);

		return true;
	}

	public async Task<bool> UserCanAccessGalleryAsync(int galleryId, string userId, bool requireDownload)
	{
		var now = DateTime.UtcNow;

		var album = await _dbContext.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.UserAccesses)
			.FirstOrDefaultAsync(x =>
				x.Id == galleryId &&
				x.AllowClientAccess &&
				!x.IsDeleted);

		if (album == null) return false;

		if (album.GalleryType == GalleryType.ClientPrintUpload && album.IsUserUploaded && album.OwnerUserId == userId)
			return !requireDownload || !_mapper.IsUserGalleryExpired(album, now);

		var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
		if (access == null) return false;

		return requireDownload ? _mapper.IsDownloadActive(access, now) : access.PreviewEnabled;
	}
}

internal static class MyClientGalleryDtoSortExtensions
{
	public static DateTime CreatedSortDate(this MyClientGalleryDto dto)
	{
		return dto.ExpiresAtUtc?.AddDays(-7) ?? DateTime.MinValue;
	}
}
