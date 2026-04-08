namespace DGVisionStudio.Domain.Entities;

public class PortfolioAlbum
{
	public int Id { get; set; }
	public int PortfolioCategoryId { get; set; }
	public PortfolioCategory? PortfolioCategory { get; set; }
	public string Slug { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? TitleEn { get; set; }
	public string? Description { get; set; }
	public string? CoverImageUrl { get; set; }
	public int DisplayOrder { get; set; }
	public int? ColumnNumber { get; set; }
	public bool IsPublished { get; set; } = true;
	public bool AllowClientAccess { get; set; } = true;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

	public ICollection<PortfolioImage> Images { get; set; } = new List<PortfolioImage>();
	public ICollection<UserAlbumAccess> UserAccesses { get; set; } = new List<UserAlbumAccess>();
}