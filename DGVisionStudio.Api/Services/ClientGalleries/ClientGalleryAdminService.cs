using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryAdminService : IClientGalleryAdminService
{
	private readonly AppDbContext _dbContext;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly IClientGalleryAccessService _accessService;
	private readonly ClientGalleryMapper _mapper;
	private readonly ClientGalleryNamingService _namingService;
	private readonly ILogger<ClientGalleryAdminService> _logger;

	public ClientGalleryAdminService(
		AppDbContext dbContext,
		UserManager<ApplicationUser> userManager,
		IClientGalleryAccessService accessService,
		ClientGalleryMapper mapper,
		ClientGalleryNamingService namingService,
		ILogger<ClientGalleryAdminService> logger)
	{
		_dbContext = dbContext;
		_userManager = userManager;
		_accessService = accessService;
		_mapper = mapper;
		_namingService = namingService;
		_logger = logger;
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

		return albums.Select(x =>
		{
			var firstAccess = x.UserAccesses
				.OrderByDescending(a => a.DownloadEnabled)
				.ThenByDescending(a => a.DownloadExpiresAtUtc)
				.FirstOrDefault();

			return _mapper.MapGalleryDto(x, now, firstAccess, isOwner: false, isAdminView: true);
		}).ToList();
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

		if (album == null) return null;

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

		var canDownload = album.GalleryType != GalleryType.ClientPrintUpload || !_mapper.IsUserGalleryExpired(album, now);

		var dto = _mapper.MapGalleryDetailsDto(album, now, firstDownloadAccess, canDownload, isOwner: false, isAdminView: true);
		dto.AvailableUsers = users;

		return dto;
	}

	public async Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request)
	{
		await using var transaction = await _dbContext.Database.BeginTransactionAsync();

		var galleryType = NormalizeGalleryType(request.GalleryType);
		var status = NormalizeGalleryStatus(galleryType, request.UserGalleryStatus);

		var categoryId = request.IsPublic
			? request.PortfolioCategoryId ?? await _namingService.EnsureClientAlbumsCategoryAsync()
			: await _namingService.EnsureClientAlbumsCategoryAsync();

		var maxDisplayOrder = await _dbContext.PortfolioAlbums
			.Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var title = request.Title.Trim();
		var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();

		var album = new PortfolioAlbum
		{
			PortfolioCategoryId = categoryId,
			Slug = await _namingService.BuildUniqueSlugAsync(titleEn ?? title),
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

		await _accessService.SyncUserAccessesAsync(album.Id, request.UserAccesses);

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

		if (album == null) return false;

		var galleryType = NormalizeGalleryType(request.GalleryType);
		var status = NormalizeGalleryStatus(galleryType, request.UserGalleryStatus);

		var title = request.Title.Trim();
		var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();

		if (!string.Equals(album.Title, title, StringComparison.Ordinal) ||
			!string.Equals(album.TitleEn, titleEn, StringComparison.Ordinal))
		{
			album.Slug = await _namingService.BuildUniqueSlugAsync(titleEn ?? title, galleryId);
		}

		album.Title = title;
		album.TitleEn = titleEn;
		album.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
		album.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
		album.AllowClientAccess = request.IsActive;
		album.IsPublished = request.IsPublic && request.IsPublished;
		album.PortfolioCategoryId = request.IsPublic
			? request.PortfolioCategoryId ?? album.PortfolioCategoryId
			: await _namingService.EnsureClientAlbumsCategoryAsync();
		album.GalleryType = galleryType;
		album.UserGalleryStatus = status;

		if (galleryType == GalleryType.Photoshoot)
		{
			album.IsUserUploaded = false;
			album.OwnerUserId = null;
			album.ExpiresAtUtc = null;
		}

		await _dbContext.SaveChangesAsync();
		await _accessService.SyncUserAccessesAsync(album.Id, request.UserAccesses);
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

		if (album == null) return false;

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
