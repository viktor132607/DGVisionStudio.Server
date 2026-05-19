using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryMapper
{
	public MyClientGalleryDto MapGalleryDto(PortfolioAlbum album, DateTime now, UserAlbumAccess? access, bool isOwner = false, bool isAdminView = false)
	{
		var canDownload = isAdminView ? true : isOwner ? !IsUserGalleryExpired(album, now) : access != null && IsDownloadActive(access, now);

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

	public ClientGalleryDetailsDto MapGalleryDetailsDto(PortfolioAlbum album, DateTime now, UserAlbumAccess? access, bool canDownload, bool isOwner = false, bool isAdminView = false)
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

	public ClientPhotoDto MapPhotoDto(PortfolioImage image, bool canDownload, int galleryId)
	{
		var previewUrl = string.IsNullOrWhiteSpace(image.ThumbnailUrl) ? image.ImageUrl : image.ThumbnailUrl!;

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

	public bool IsDownloadActive(UserAlbumAccess access, DateTime now)
	{
		if (!access.DownloadEnabled) return false;
		if (!access.DownloadExpiresAtUtc.HasValue) return true;
		return access.DownloadExpiresAtUtc.Value >= now;
	}

	public bool IsExpired(UserAlbumAccess access, DateTime now) =>
		access.DownloadEnabled && access.DownloadExpiresAtUtc.HasValue && access.DownloadExpiresAtUtc.Value < now;

	public bool IsUserGalleryExpired(PortfolioAlbum album, DateTime now) =>
		album.GalleryType == GalleryType.ClientPrintUpload &&
		album.IsUserUploaded &&
		album.ExpiresAtUtc.HasValue &&
		album.ExpiresAtUtc.Value < now;

	public UserClientGalleryStatus GetEffectiveStatus(PortfolioAlbum album, DateTime now)
	{
		if (album.GalleryType == GalleryType.ClientPrintUpload && IsUserGalleryExpired(album, now))
			return UserClientGalleryStatus.Expired;

		return album.UserGalleryStatus;
	}

	private int? GetRemainingDays(UserAlbumAccess access, DateTime now)
	{
		if (!access.DownloadEnabled || !access.DownloadExpiresAtUtc.HasValue) return null;
		return Math.Max(0, (access.DownloadExpiresAtUtc.Value.Date - now.Date).Days);
	}

	private int? GetRemainingLifetimeDays(PortfolioAlbum album, DateTime now)
	{
		if (album.GalleryType != GalleryType.ClientPrintUpload) return null;
		if (!album.ExpiresAtUtc.HasValue) return null;
		return Math.Max(0, (album.ExpiresAtUtc.Value.Date - now.Date).Days);
	}
}
