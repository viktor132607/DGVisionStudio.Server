using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryAccessService : IClientGalleryAccessService
{
	private readonly AppDbContext _dbContext;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<ClientGalleryAccessService> _logger;

	public ClientGalleryAccessService(
		AppDbContext dbContext,
		UserManager<ApplicationUser> userManager,
		ILogger<ClientGalleryAccessService> logger)
	{
		_dbContext = dbContext;
		_userManager = userManager;
		_logger = logger;
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

		if (album == null) return false;

		var user = await _userManager.FindByEmailAsync(request.UserEmail.Trim());
		if (user == null) return false;

		// Fix: public portfolio albums can receive client access even if they were not client galleries before.
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

		if (access == null) return false;

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

		if (access == null) return false;

		_dbContext.UserAlbumAccesses.Remove(access);
		await _dbContext.SaveChangesAsync();
		await transaction.CommitAsync();

		_logger.LogWarning(
			"Gallery access removed. GalleryId: {GalleryId}, UserId: {UserId}",
			galleryId,
			userId);

		return true;
	}

	public async Task SyncUserAccessesAsync(int galleryId, List<GalleryUserAccessDto>? requestedAccesses)
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

			if (string.IsNullOrWhiteSpace(userId)) continue;

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
}
