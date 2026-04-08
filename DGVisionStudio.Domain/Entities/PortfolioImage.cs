namespace DGVisionStudio.Domain.Entities;

public class PortfolioImage
{
	public int Id { get; set; }
	public int PortfolioAlbumId { get; set; }
	public PortfolioAlbum? PortfolioAlbum { get; set; }
	public string ImageUrl { get; set; } = string.Empty;
	public string? ThumbnailUrl { get; set; }
	public string? AltText { get; set; }
	public string? Caption { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsCover { get; set; }
	public bool IsPublished { get; set; } = true;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}