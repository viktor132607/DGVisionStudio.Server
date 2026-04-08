namespace DGVisionStudio.Application.DTOs;

public class CreatePortfolioImageRequest
{
	public int PortfolioAlbumId { get; set; }
	public string ImageUrl { get; set; } = string.Empty;
	public string? ThumbnailUrl { get; set; }
	public string? AltText { get; set; }
	public string? Caption { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsCover { get; set; }
	public bool IsPublished { get; set; } = true;
	public int? Width { get; set; }
	public int? Height { get; set; }
}