using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services;

public class ExpiredGalleryCleanupService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<ExpiredGalleryCleanupService> _logger;

	public ExpiredGalleryCleanupService(
		IServiceScopeFactory scopeFactory,
		ILogger<ExpiredGalleryCleanupService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await CleanupExpiredDownloadsAsync(stoppingToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while cleaning up expired album downloads.");
			}

			await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
		}
	}

	private async Task CleanupExpiredDownloadsAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();

		var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var now = DateTime.UtcNow;

		var expiredAccesses = await dbContext.UserAlbumAccesses
			.Where(x =>
				x.DownloadEnabled &&
				x.DownloadExpiresAtUtc.HasValue &&
				x.DownloadExpiresAtUtc.Value < now)
			.ToListAsync(cancellationToken);

		if (expiredAccesses.Count == 0)
			return;

		foreach (var access in expiredAccesses)
		{
			access.DownloadEnabled = false;
			access.DownloadExpiresAtUtc = null;
		}

		await dbContext.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Expired album download cleanup completed. Processed {Count} expired access records.",
			expiredAccesses.Count);
	}
}