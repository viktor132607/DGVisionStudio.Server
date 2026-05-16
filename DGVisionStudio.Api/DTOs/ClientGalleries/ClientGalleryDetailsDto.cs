using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class ClientGalleryDetailsDto
{
	public int Id { get; set; }

	public string Title { get; set; } = string.Empty;

	public string? TitleEn { get; set; }

	public string? Description { get; set; }

	public string? CoverImageUrl { get; set; }

	public bool IsActive { get; set; }

	public bool IsPublic { get; set; }

	public bool IsPublished { get; set; }

	public int? PortfolioCategoryId { get; set; }

	public bool PreviewEnabled { get; set; }

	public bool DownloadEnabled { get; set; }

	public DateTime? DownloadExpiresAtUtc { get; set; }

	public int? RemainingDownloadDays { get; set; }

	public bool IsExpired { get; set; }

	public GalleryType GalleryType { get; set; }

	public bool IsUserUploaded { get; set; }

	public string? OwnerUserId { get; set; }

	public string? OwnerEmail { get; set; }

	public DateTime? ExpiresAtUtc { get; set; }

	public int? RemainingLifetimeDays { get; set; }

	public UserClientGalleryStatus UserGalleryStatus { get; set; }

	public List<AdminGalleryUserOptionDto> AvailableUsers { get; set; } = new();

	public List<GalleryUserAccessDto> UserAccesses { get; set; } = new();

	public List<ClientPhotoDto> Photos { get; set; } = new();
}