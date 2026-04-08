namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class AdminUpdateClientGalleryRequest
{
	public string Title { get; set; } = string.Empty;

	public string? TitleEn { get; set; }

	public string? Description { get; set; }

	public string? CoverImageUrl { get; set; }

	public bool IsActive { get; set; }

	public bool IsPublic { get; set; }

	public bool IsPublished { get; set; }

	public int? PortfolioCategoryId { get; set; }

	public List<GalleryUserAccessDto> UserAccesses { get; set; } = new();
}