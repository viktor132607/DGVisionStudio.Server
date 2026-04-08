namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class MyClientGalleryDto
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

	public string? PortfolioCategoryName { get; set; }

	public string? PortfolioCategoryNameEn { get; set; }

	public bool PreviewEnabled { get; set; }

	public bool DownloadEnabled { get; set; }

	public DateTime? DownloadExpiresAtUtc { get; set; }

	public int? RemainingDownloadDays { get; set; }

	public bool IsExpired { get; set; }
}