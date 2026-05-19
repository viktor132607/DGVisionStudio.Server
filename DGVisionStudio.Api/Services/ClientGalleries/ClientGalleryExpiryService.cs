using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryExpiryService : IClientGalleryExpiryService
{
	private readonly AppDbContext _dbContext;
	private readonly ILogger<ClientGalleryExpiryService> _logger;

	public ClientGalleryExpiryService(
		AppDbContext dbContext,
		ILogger<ClientGalleryExpiryService> logger)
	{
		_dbContext = dbContext;
		_logger = logger;
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

		_logger.LogInformation("Expired user galleries marked. Count: {Count}", galleries.Count);

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
}
