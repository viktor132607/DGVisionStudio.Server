using System.Text.RegularExpressions;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services;

public class ClientGalleryService : IClientGalleryService
{
	private const long MaxImageUploadSizeBytes = 15 * 1024 * 1024;
	private const int MaxUserUploadedGalleries = 10;
	private const int UserUploadedGalleryLifetimeDays = 7;

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

	private readonly AppDbContext _dbContext;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly IFileStorageService _fileStorageService;
	private readonly ILogger<ClientGalleryService> _logger;

	public ClientGalleryService(
		AppDbContext dbContext,
		UserManager<ApplicationUser> userManager,
		IFileStorageService fileStorageService,
		ILogger<ClientGalleryService> logger)
	{
		_dbContext = dbContext;
		_userManager = userManager;
		_fileStorageService = fileStorageService;
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
			result.Add(MapGalleryDto(item.Album, now, item.Access));
		}

		foreach (var album in ownedAlbums)
		{
			if (result.Any(x => x.Id == album.Id))
				continue;

			result.Add(MapGalleryDto(album, now, null, isOwner: true));
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

		if (album == null)
			return null;

		var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
		var isOwner = album.GalleryType == GalleryType.ClientPrintUpload && album.IsUserUploaded && album.OwnerUserId == userId;

		if (!isOwner && access == null)
			return null;

		var canDownload = isOwner
			? !IsUserGalleryExpired(album, now)
			: access != null && IsDownloadActive(access, now);

		return MapGalleryDetailsDto(album, now, access, canDownload, isOwner);
	}

	public async Task<List<MyClientGalleryDto>> GetAllGalleriesAsync()
	{
		var now = DateTime.UtcNow;

		var albums = await _dbContext.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.PortfolioCategory)
			.Include(x => x.OwnerUser)
			.Include(x => x.UserAccesses)
			.Where(x => !x.IsDeleted)
			.OrderByDescending(x => x.CreatedAtUtc)
			.ThenByDescending(x => x.Id)
			.ToListAsync();

		return albums
			.Select(x =>
			{
				var firstAccess = x.UserAccesses
					.OrderByDescending(a => a.DownloadEnabled)
					.ThenByDescending(a => a.DownloadExpiresAtUtc)
					.FirstOrDefault();

				return MapGalleryDto(x, now, firstAccess, isOwner: false, isAdminView: true);
			})
			.ToList();
	}

	public async Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId)
	{
		var now = DateTime.UtcNow;

		var album = await _dbContext.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.PortfolioCategory)
			.Include(x => x.OwnerUser)
			.Include(x => x.Images)
			.Include(x => x.UserAccesses)
				.ThenInclude(x => x.User)
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null)
			return null;

		var users = await _userManager.Users
			.AsNoTracking()
			.OrderBy(x => x.Email)
			.Select(x => new AdminGalleryUserOptionDto
			{
				Id = x.Id,
				Email = x.Email ?? string.Empty
			})
			.ToListAsync();

		var firstDownloadAccess = album.UserAccesses
			.OrderByDescending(x => x.DownloadEnabled)
			.ThenByDescending(x => x.DownloadExpiresAtUtc)
			.FirstOrDefault();

		var canDownload = album.GalleryType != GalleryType.ClientPrintUpload || !IsUserGalleryExpired(album, now);

		var dto = MapGalleryDetailsDto(album, now, firstDownloadAccess, canDownload, isOwner: false, isAdminView: true);
		dto.AvailableUsers = users;

		return dto;
	}

	public async Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var galleryType = NormalizeGalleryType(request.GalleryType);
		var status = NormalizeGalleryStatus(galleryType, request.UserGalleryStatus);

		var categoryId = request.IsPublic
			? request.PortfolioCategoryId ?? await EnsureClientAlbumsCategoryAsync()
			: await EnsureClientAlbumsCategoryAsync();

		var maxDisplayOrder = await _dbContext.PortfolioAlbums
			.Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var title = request.Title.Trim();
		var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();

		var album = new PortfolioAlbum
		{
			PortfolioCategoryId = categoryId,
			Slug = await BuildUniqueSlugAsync(titleEn ?? title),
			Title = title,
			TitleEn = titleEn,
			Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
			CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim(),
			DisplayOrder = maxDisplayOrder + 1,
			IsPublished = request.IsPublic && request.IsPublished,
			AllowClientAccess = request.IsActive,
			GalleryType = galleryType,
			IsUserUploaded = false,
			UserGalleryStatus = status,
			IsDeleted = false,
			DeletedAtUtc = null,
			CreatedAtUtc = DateTime.UtcNow
		};

		_dbContext.PortfolioAlbums.Add(album);
		await _dbContext.SaveChangesAsync();

		await SyncUserAccessesAsync(album.Id, request.UserAccesses);

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin client gallery created. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, Status: {Status}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, IsActive: {IsActive}",
			album.Id,
			album.Title,
			album.GalleryType,
			album.UserGalleryStatus,
			request.IsPublic,
			request.IsPublished,
			request.IsActive);

		return album.Id;
	}

	public async Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.UserAccesses)
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null)
			return false;

		var galleryType = NormalizeGalleryType(request.GalleryType);
		var status = NormalizeGalleryStatus(galleryType, request.UserGalleryStatus);

		var title = request.Title.Trim();
		var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();

		if (!string.Equals(album.Title, title, StringComparison.Ordinal) ||
			!string.Equals(album.TitleEn, titleEn, StringComparison.Ordinal))
		{
			album.Slug = await BuildUniqueSlugAsync(titleEn ?? title, galleryId);
		}

		album.Title = title;
		album.TitleEn = titleEn;
		album.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
		album.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
		album.AllowClientAccess = request.IsActive;
		album.IsPublished = request.IsPublic && request.IsPublished;
		album.PortfolioCategoryId = request.IsPublic
			? request.PortfolioCategoryId ?? album.PortfolioCategoryId
			: await EnsureClientAlbumsCategoryAsync();
		album.GalleryType = galleryType;
		album.UserGalleryStatus = status;

		if (galleryType == GalleryType.Photoshoot)
		{
			album.IsUserUploaded = false;
			album.OwnerUserId = null;
			album.ExpiresAtUtc = null;
		}

		await _dbContext.SaveChangesAsync();

		await SyncUserAccessesAsync(album.Id, request.UserAccesses);

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin client gallery updated. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, Status: {Status}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, IsActive: {IsActive}",
			album.Id,
			album.Title,
			album.GalleryType,
			album.UserGalleryStatus,
			request.IsPublic,
			request.IsPublished,
			request.IsActive);

		return true;
	}

	public async Task<bool> DeleteGalleryAsync(int galleryId)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.Include(x => x.UserAccesses)
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null)
			return false;

		var now = DateTime.UtcNow;
		var imageCount = album.Images.Count;
		var userAccessCount = album.UserAccesses.Count;
		var isUserUploaded = album.IsUserUploaded;
		var ownerUserId = album.OwnerUserId;

		album.IsDeleted = true;
		album.DeletedAtUtc = now;
		album.AllowClientAccess = false;
		album.IsPublished = false;

		foreach (var image in album.Images)
		{
			image.IsDeleted = true;
			image.DeletedAtUtc = now;
			image.IsPublished = false;
			image.IsCover = false;
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogWarning(
			"Client gallery soft deleted. GalleryId: {GalleryId}, ImageCount: {ImageCount}, UserAccessCount: {UserAccessCount}, IsUserUploaded: {IsUserUploaded}, OwnerUserId: {OwnerUserId}",
			galleryId,
			imageCount,
			userAccessCount,
			isUserUploaded,
			ownerUserId);

		return true;
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
		if (string.IsNullOrWhiteSpace(title))
			return null;

		var categoryId = await EnsureClientAlbumsCategoryAsync();

		var maxDisplayOrder = await _dbContext.PortfolioAlbums
			.Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var album = new PortfolioAlbum
		{
			PortfolioCategoryId = categoryId,
			Slug = await BuildUniqueSlugAsync(title),
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
		await ValidateUploadedImageAsync(file);

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

		if (album == null)
			return null;

		if (IsUserGalleryExpired(album, now))
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

		return MapPhotoDto(photo, true, galleryId);
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

		if (album == null)
			return false;

		if (album.GalleryType == GalleryType.ClientPrintUpload && album.IsUserUploaded && album.OwnerUserId == userId)
			return !requireDownload || !IsUserGalleryExpired(album, now);

		var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
		if (access == null)
			return false;

		return requireDownload
			? IsDownloadActive(access, now)
			: access.PreviewEnabled;
	}

	public async Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(int galleryId, int photoId, string userId, bool isAdmin)
	{
		var now = DateTime.UtcNow;

		var photo = await _dbContext.PortfolioImages
			.AsNoTracking()
			.Include(x => x.PortfolioAlbum)
				.ThenInclude(x => x!.UserAccesses)
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
		else if (album.GalleryType == GalleryType.ClientPrintUpload && album.IsUserUploaded && album.OwnerUserId == userId && !IsUserGalleryExpired(album, now))
		{
			allowed = true;
		}
		else
		{
			var access = album.UserAccesses.FirstOrDefault(x => x.UserId == userId);
			allowed = access != null && IsDownloadActive(access, now);
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
		var fileName = $"{Slugify(album.Title)}-{photo.Id}{extension}";

		_logger.LogInformation(
			"Photo download opened. GalleryId: {GalleryId}, PhotoId: {PhotoId}, UserId: {UserId}, IsAdmin: {IsAdmin}, FileName: {FileName}",
			galleryId,
			photoId,
			userId,
			isAdmin,
			fileName);

		return (stream, contentType, fileName);
	}

	public async Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId)
	{
		return await _dbContext.UserAlbumAccesses
			.AsNoTracking()
			.Include(x => x.User)
			.Where(x => x.PortfolioAlbumId == galleryId && !x.PortfolioAlbum.IsDeleted)
			.OrderBy(x => x.User.Email)
			.Select(x => new GalleryUserAccessDto
			{
				UserId = x.UserId,
				Email = x.User.Email ?? string.Empty,
				PreviewEnabled = x.PreviewEnabled,
				DownloadEnabled = x.DownloadEnabled,
				DownloadExpiresAtUtc = x.DownloadExpiresAtUtc
			})
			.ToListAsync();
	}

	public async Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null)
			return false;

		var user = await _userManager.FindByEmailAsync(request.UserEmail.Trim());
		if (user == null)
			return false;

		album.AllowClientAccess = true;

		var access = await _dbContext.UserAlbumAccesses
			.FirstOrDefaultAsync(x => x.PortfolioAlbumId == galleryId && x.UserId == user.Id);

		if (access == null)
		{
			access = new UserAlbumAccess
			{
				PortfolioAlbumId = galleryId,
				UserId = user.Id,
				PreviewEnabled = request.PreviewEnabled,
				DownloadEnabled = request.DownloadEnabled,
				DownloadExpiresAtUtc = request.DownloadExpiresAtUtc
			};

			_dbContext.UserAlbumAccesses.Add(access);
		}
		else
		{
			access.PreviewEnabled = request.PreviewEnabled;
			access.DownloadEnabled = request.DownloadEnabled;
			access.DownloadExpiresAtUtc = request.DownloadExpiresAtUtc;
		}

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Gallery access granted or updated. GalleryId: {GalleryId}, UserId: {UserId}, UserEmail: {UserEmail}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}",
			galleryId,
			user.Id,
			user.Email,
			request.PreviewEnabled,
			request.DownloadEnabled,
			request.DownloadExpiresAtUtc);

		return true;
	}

	public async Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var access = await _dbContext.UserAlbumAccesses
			.Include(x => x.PortfolioAlbum)
			.FirstOrDefaultAsync(x =>
				x.PortfolioAlbumId == galleryId &&
				x.UserId == userId &&
				!x.PortfolioAlbum.IsDeleted);

		if (access == null)
			return false;

		access.PreviewEnabled = request.PreviewEnabled;
		access.DownloadEnabled = request.DownloadEnabled;
		access.DownloadExpiresAtUtc = request.DownloadExpiresAtUtc;

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Gallery access updated. GalleryId: {GalleryId}, UserId: {UserId}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}",
			galleryId,
			userId,
			request.PreviewEnabled,
			request.DownloadEnabled,
			request.DownloadExpiresAtUtc);

		return true;
	}

	public async Task<bool> RemoveAccessAsync(int galleryId, string userId)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var access = await _dbContext.UserAlbumAccesses
			.Include(x => x.PortfolioAlbum)
			.FirstOrDefaultAsync(x =>
				x.PortfolioAlbumId == galleryId &&
				x.UserId == userId &&
				!x.PortfolioAlbum.IsDeleted);

		if (access == null)
			return false;

		_dbContext.UserAlbumAccesses.Remove(access);
		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogWarning(
			"Gallery access removed. GalleryId: {GalleryId}, UserId: {UserId}",
			galleryId,
			userId);

		return true;
	}

	public async Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file)
	{
		await ValidateUploadedImageAsync(file);

		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null)
			return null;

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

		return MapPhotoDto(photo, true, galleryId);
	}

	public async Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var photo = await _dbContext.PortfolioImages
			.Include(x => x.PortfolioAlbum)
				.ThenInclude(x => x.Images)
			.FirstOrDefaultAsync(x =>
				x.Id == photoId &&
				x.PortfolioAlbumId == galleryId &&
				!x.IsDeleted &&
				!x.PortfolioAlbum!.IsDeleted);

		if (photo == null)
			return null;

		photo.AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim();
		photo.Caption = string.IsNullOrWhiteSpace(request.Caption)
			? (string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim())
			: request.Caption.Trim();
		photo.DisplayOrder = request.DisplayOrder ?? photo.DisplayOrder;

		if (request.IsPublished.HasValue)
		{
			photo.IsPublished = request.IsPublished.Value;
		}

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

		return MapPhotoDto(photo, true, galleryId);
	}

	public async Task<bool> DeletePhotoAsync(int galleryId, int photoId)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var album = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (album == null)
			return false;

		var photo = album.Images.FirstOrDefault(x => x.Id == photoId && !x.IsDeleted);
		if (photo == null)
			return false;

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

		if (album == null)
			return false;

		var matchingPhoto = album.Images.FirstOrDefault(x =>
			!x.IsDeleted &&
			(string.Equals(x.ThumbnailUrl, coverImageUrl, StringComparison.OrdinalIgnoreCase) ||
			 string.Equals(x.ImageUrl, coverImageUrl, StringComparison.OrdinalIgnoreCase)));

		if (matchingPhoto == null)
			return false;

		foreach (var image in album.Images.Where(x => !x.IsDeleted))
		{
			image.IsCover = image.Id == matchingPhoto.Id;
		}

		album.CoverImageUrl = matchingPhoto.ThumbnailUrl ?? matchingPhoto.ImageUrl;

		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogInformation(
			"Gallery cover image changed. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}",
			galleryId,
			coverImageUrl);

		return true;
	}

	public async Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var albumExists = await _dbContext.PortfolioAlbums
			.AnyAsync(x => x.Id == galleryId && !x.IsDeleted);

		if (!albumExists)
			return false;

		var photos = await _dbContext.PortfolioImages
			.Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
			.ToListAsync();

		if (photos.Count == 0)
			return false;

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

	public async Task<int> MarkExpiredUserGalleriesAsync(CancellationToken cancellationToken = default)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

		var now = DateTime.UtcNow;

		var galleries = await _dbContext.PortfolioAlbums
			.Where(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				!x.IsDeleted &&
				x.ExpiresAtUtc != null &&
				x.ExpiresAtUtc < now &&
				x.UserGalleryStatus != UserClientGalleryStatus.Expired)
			.ToListAsync(cancellationToken);

		foreach (var gallery in galleries)
		{
			gallery.UserGalleryStatus = UserClientGalleryStatus.Expired;
			gallery.AllowClientAccess = false;
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		_logger.LogInformation(
			"Expired user galleries marked. Count: {Count}",
			galleries.Count);

		return galleries.Count;
	}

	public async Task<int> DeleteExpiredUserGalleriesAsync(CancellationToken cancellationToken = default)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

		var deleteBeforeUtc = DateTime.UtcNow.AddDays(-1);
		var now = DateTime.UtcNow;

		var galleries = await _dbContext.PortfolioAlbums
			.Include(x => x.Images)
			.Include(x => x.UserAccesses)
			.Where(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				!x.IsDeleted &&
				x.ExpiresAtUtc != null &&
				x.ExpiresAtUtc < deleteBeforeUtc)
			.ToListAsync(cancellationToken);

		foreach (var gallery in galleries)
		{
			gallery.IsDeleted = true;
			gallery.DeletedAtUtc = now;
			gallery.AllowClientAccess = false;
			gallery.IsPublished = false;
			gallery.UserGalleryStatus = UserClientGalleryStatus.Expired;

			foreach (var image in gallery.Images)
			{
				image.IsDeleted = true;
				image.DeletedAtUtc = now;
				image.IsPublished = false;
				image.IsCover = false;
			}
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		_logger.LogWarning(
			"Expired user galleries soft deleted. Count: {Count}, DeleteBeforeUtc: {DeleteBeforeUtc}",
			galleries.Count,
			deleteBeforeUtc);

		return galleries.Count;
	}

	private async Task SyncUserAccessesAsync(int galleryId, List<GalleryUserAccessDto>? requestedAccesses)
	{
		var normalized = requestedAccesses ?? new List<GalleryUserAccessDto>();

		var existing = await _dbContext.UserAlbumAccesses
			.Where(x => x.PortfolioAlbumId == galleryId)
			.ToListAsync();

		var desiredByUserId = new Dictionary<string, GalleryUserAccessDto>(StringComparer.OrdinalIgnoreCase);

		foreach (var item in normalized)
		{
			string? userId = null;

			if (!string.IsNullOrWhiteSpace(item.UserId))
			{
				userId = item.UserId.Trim();
			}
			else if (!string.IsNullOrWhiteSpace(item.Email))
			{
				var user = await _userManager.FindByEmailAsync(item.Email.Trim());
				userId = user?.Id;
			}

			if (string.IsNullOrWhiteSpace(userId))
				continue;

			desiredByUserId[userId] = item;
		}

		foreach (var existingAccess in existing)
		{
			if (!desiredByUserId.ContainsKey(existingAccess.UserId))
			{
				_dbContext.UserAlbumAccesses.Remove(existingAccess);
			}
		}

		foreach (var pair in desiredByUserId)
		{
			var userId = pair.Key;
			var item = pair.Value;

			var access = existing.FirstOrDefault(x => x.UserId == userId);
			if (access == null)
			{
				access = new UserAlbumAccess
				{
					PortfolioAlbumId = galleryId,
					UserId = userId
				};

				_dbContext.UserAlbumAccesses.Add(access);
			}

			access.PreviewEnabled = item.PreviewEnabled;
			access.DownloadEnabled = item.DownloadEnabled;
			access.DownloadExpiresAtUtc = item.DownloadExpiresAtUtc;
		}

		await _dbContext.SaveChangesAsync();
	}

	private async Task<int> EnsureClientAlbumsCategoryAsync()
	{
		var existing = await _dbContext.PortfolioCategories
			.FirstOrDefaultAsync(x => x.Key == "client-galleries" && !x.IsDeleted);

		if (existing != null)
			return existing.Id;

		var maxOrder = await _dbContext.PortfolioCategories
			.Where(x => !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var category = new PortfolioCategory
		{
			Key = "client-galleries",
			Name = "Клиентски албуми",
			NameEn = "Client Galleries",
			Description = "Albums created from the client gallery admin.",
			DisplayOrder = maxOrder + 1,
			IsActive = false,
			IsDeleted = false,
			DeletedAtUtc = null
		};

		_dbContext.PortfolioCategories.Add(category);
		await _dbContext.SaveChangesAsync();
		return category.Id;
	}

	private async Task<string> BuildUniqueSlugAsync(string title, int? currentAlbumId = null)
	{
		var baseSlug = Slugify(title);
		var slug = baseSlug;
		var index = 2;

		while (await _dbContext.PortfolioAlbums.AnyAsync(x => x.Slug == slug && x.Id != currentAlbumId && !x.IsDeleted))
		{
			slug = $"{baseSlug}-{index}";
			index++;
		}

		return slug;
	}

	private static string Slugify(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return Guid.NewGuid().ToString("N");

		var slug = value.Trim().ToLowerInvariant();
		slug = Regex.Replace(slug, @"[^a-z0-9а-я]+", "-");
		slug = Regex.Replace(slug, @"-+", "-").Trim('-');

		return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
	}

	private static async Task ValidateUploadedImageAsync(IFormFile file)
	{
		if (file == null || file.Length == 0)
			throw new ArgumentException("File is required.");

		if (file.Length > MaxImageUploadSizeBytes)
			throw new ArgumentException("File size cannot exceed 15 MB.");

		var originalFileName = Path.GetFileName(file.FileName);
		if (string.IsNullOrWhiteSpace(originalFileName))
			throw new ArgumentException("Invalid file name.");

		var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(extension))
			throw new ArgumentException("File extension is required.");

		if (DangerousExtensions.Contains(extension))
			throw new ArgumentException("File type is not allowed.");

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
		{
			return header[0] == 0xFF &&
				   header[1] == 0xD8 &&
				   header[2] == 0xFF;
		}

		if (extension == ".png" && bytesRead >= 8)
		{
			return header[0] == 0x89 &&
				   header[1] == 0x50 &&
				   header[2] == 0x4E &&
				   header[3] == 0x47 &&
				   header[4] == 0x0D &&
				   header[5] == 0x0A &&
				   header[6] == 0x1A &&
				   header[7] == 0x0A;
		}

		if (extension == ".webp" && bytesRead >= 12)
		{
			return header[0] == 0x52 &&
				   header[1] == 0x49 &&
				   header[2] == 0x46 &&
				   header[3] == 0x46 &&
				   header[8] == 0x57 &&
				   header[9] == 0x45 &&
				   header[10] == 0x42 &&
				   header[11] == 0x50;
		}

		return false;
	}

	private static MyClientGalleryDto MapGalleryDto(
		PortfolioAlbum album,
		DateTime now,
		UserAlbumAccess? access,
		bool isOwner = false,
		bool isAdminView = false)
	{
		var canDownload = isAdminView
			? true
			: isOwner
				? !IsUserGalleryExpired(album, now)
				: access != null && IsDownloadActive(access, now);

		return new MyClientGalleryDto
		{
			Id = album.Id,
			Title = album.Title,
			TitleEn = album.TitleEn,
			Description = album.Description,
			CoverImageUrl = album.CoverImageUrl,
			IsActive = album.AllowClientAccess,
			IsPublic = album.PortfolioCategory != null && album.PortfolioCategory.Key != "client-galleries",
			IsPublished = album.IsPublished,
			PortfolioCategoryId = album.PortfolioCategoryId,
			PortfolioCategoryName = album.PortfolioCategory?.Name,
			PortfolioCategoryNameEn = album.PortfolioCategory?.NameEn,
			PreviewEnabled = isOwner || access?.PreviewEnabled == true || isAdminView,
			DownloadEnabled = canDownload,
			DownloadExpiresAtUtc = access?.DownloadExpiresAtUtc,
			RemainingDownloadDays = access != null ? GetRemainingDays(access, now) : null,
			IsExpired = access != null && IsExpired(access, now),
			GalleryType = album.GalleryType,
			IsUserUploaded = album.IsUserUploaded,
			OwnerUserId = album.OwnerUserId,
			OwnerEmail = album.OwnerUser?.Email,
			ExpiresAtUtc = album.ExpiresAtUtc,
			RemainingLifetimeDays = GetRemainingLifetimeDays(album, now),
			UserGalleryStatus = GetEffectiveStatus(album, now)
		};
	}

	private static ClientGalleryDetailsDto MapGalleryDetailsDto(
		PortfolioAlbum album,
		DateTime now,
		UserAlbumAccess? access,
		bool canDownload,
		bool isOwner = false,
		bool isAdminView = false)
	{
		return new ClientGalleryDetailsDto
		{
			Id = album.Id,
			Title = album.Title,
			TitleEn = album.TitleEn,
			Description = album.Description,
			CoverImageUrl = album.CoverImageUrl,
			IsActive = album.AllowClientAccess,
			IsPublic = album.PortfolioCategory != null && album.PortfolioCategory.Key != "client-galleries",
			IsPublished = album.IsPublished,
			PortfolioCategoryId = album.PortfolioCategoryId,
			PreviewEnabled = isOwner || access?.PreviewEnabled == true || isAdminView,
			DownloadEnabled = canDownload,
			DownloadExpiresAtUtc = access?.DownloadExpiresAtUtc,
			RemainingDownloadDays = access != null ? GetRemainingDays(access, now) : null,
			IsExpired = access != null && IsExpired(access, now),
			GalleryType = album.GalleryType,
			IsUserUploaded = album.IsUserUploaded,
			OwnerUserId = album.OwnerUserId,
			OwnerEmail = album.OwnerUser?.Email,
			ExpiresAtUtc = album.ExpiresAtUtc,
			RemainingLifetimeDays = GetRemainingLifetimeDays(album, now),
			UserGalleryStatus = GetEffectiveStatus(album, now),
			UserAccesses = album.UserAccesses
				.OrderBy(x => x.User.Email)
				.Select(x => new GalleryUserAccessDto
				{
					UserId = x.UserId,
					Email = x.User.Email ?? string.Empty,
					PreviewEnabled = x.PreviewEnabled,
					DownloadEnabled = x.DownloadEnabled,
					DownloadExpiresAtUtc = x.DownloadExpiresAtUtc
				})
				.ToList(),
			Photos = album.Images
				.Where(x => !x.IsDeleted && (x.IsPublished || isAdminView))
				.OrderBy(x => x.DisplayOrder)
				.ThenBy(x => x.Id)
				.Select(x => MapPhotoDto(x, canDownload, album.Id))
				.ToList()
		};
	}

	private static ClientPhotoDto MapPhotoDto(PortfolioImage image, bool canDownload, int galleryId)
	{
		var previewUrl = string.IsNullOrWhiteSpace(image.ThumbnailUrl)
			? image.ImageUrl
			: image.ThumbnailUrl!;

		return new ClientPhotoDto
		{
			Id = image.Id,
			PreviewUrl = previewUrl,
			OriginalUrl = null,
			DownloadUrl = canDownload ? $"/api/client-galleries/{galleryId}/photos/{image.Id}/download" : null,
			AltText = image.AltText,
			Caption = image.Caption,
			CanDownload = canDownload && !string.IsNullOrWhiteSpace(image.ImageUrl),
			DisplayOrder = image.DisplayOrder,
			Description = image.Caption,
			IsPublished = image.IsPublished,
			ShowInPublicGallery = false,
			VisibleToAllAuthorizedUsers = true,
			AllowedUserIds = new List<string>()
		};
	}

	private async Task DeleteAlbumFilesAsync(PortfolioAlbum album)
	{
		foreach (var image in album.Images)
		{
			await DeletePhotoFilesAsync(image);
		}
	}

	private async Task DeletePhotoFilesAsync(PortfolioImage image)
	{
		if (!string.IsNullOrWhiteSpace(image.ImageUrl))
			await _fileStorageService.DeleteFileAsync(image.ImageUrl);

		if (!string.IsNullOrWhiteSpace(image.ThumbnailUrl) &&
			!string.Equals(image.ThumbnailUrl, image.ImageUrl, StringComparison.OrdinalIgnoreCase))
		{
			await _fileStorageService.DeleteFileAsync(image.ThumbnailUrl);
		}
	}

	private static bool IsDownloadActive(UserAlbumAccess access, DateTime now)
	{
		if (!access.DownloadEnabled)
			return false;

		if (!access.DownloadExpiresAtUtc.HasValue)
			return true;

		return access.DownloadExpiresAtUtc.Value >= now;
	}

	private static bool IsExpired(UserAlbumAccess access, DateTime now)
	{
		return access.DownloadEnabled &&
			   access.DownloadExpiresAtUtc.HasValue &&
			   access.DownloadExpiresAtUtc.Value < now;
	}

	private static bool IsUserGalleryExpired(PortfolioAlbum album, DateTime now)
	{
		return album.GalleryType == GalleryType.ClientPrintUpload &&
			   album.IsUserUploaded &&
			   album.ExpiresAtUtc.HasValue &&
			   album.ExpiresAtUtc.Value < now;
	}

	private static UserClientGalleryStatus GetEffectiveStatus(PortfolioAlbum album, DateTime now)
	{
		if (album.GalleryType == GalleryType.ClientPrintUpload && IsUserGalleryExpired(album, now))
			return UserClientGalleryStatus.Expired;

		return album.UserGalleryStatus;
	}

	private static int? GetRemainingDays(UserAlbumAccess access, DateTime now)
	{
		if (!access.DownloadEnabled || !access.DownloadExpiresAtUtc.HasValue)
			return null;

		return Math.Max(0, (access.DownloadExpiresAtUtc.Value.Date - now.Date).Days);
	}

	private static int? GetRemainingLifetimeDays(PortfolioAlbum album, DateTime now)
	{
		if (album.GalleryType != GalleryType.ClientPrintUpload)
			return null;

		if (!album.ExpiresAtUtc.HasValue)
			return null;

		return Math.Max(0, (album.ExpiresAtUtc.Value.Date - now.Date).Days);
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

	private static GalleryType NormalizeGalleryType(GalleryType galleryType)
	{
		return galleryType switch
		{
			GalleryType.Photoshoot => GalleryType.Photoshoot,
			GalleryType.ClientPrintUpload => GalleryType.ClientPrintUpload,
			_ => GalleryType.Photoshoot
		};
	}

	private static UserClientGalleryStatus NormalizeGalleryStatus(GalleryType galleryType, UserClientGalleryStatus status)
	{
		if (galleryType == GalleryType.ClientPrintUpload)
		{
			return status switch
			{
				UserClientGalleryStatus.Pending => UserClientGalleryStatus.Pending,
				UserClientGalleryStatus.Processed => UserClientGalleryStatus.Processed,
				UserClientGalleryStatus.Expired => UserClientGalleryStatus.Expired,
				_ => UserClientGalleryStatus.Pending
			};
		}

		return status switch
		{
			UserClientGalleryStatus.PhotoshootUploaded => UserClientGalleryStatus.PhotoshootUploaded,
			UserClientGalleryStatus.PhotoshootInProgress => UserClientGalleryStatus.PhotoshootInProgress,
			UserClientGalleryStatus.PhotoshootReadyForPickup => UserClientGalleryStatus.PhotoshootReadyForPickup,
			UserClientGalleryStatus.PhotoshootCancelled => UserClientGalleryStatus.PhotoshootCancelled,
			_ => UserClientGalleryStatus.PhotoshootUploaded
		};
	}
}

internal static class MyClientGalleryDtoSortExtensions
{
	public static DateTime CreatedSortDate(this MyClientGalleryDto dto)
	{
		return dto.ExpiresAtUtc?.AddDays(-7) ?? DateTime.MinValue;
	}
}