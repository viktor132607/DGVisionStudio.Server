namespace DGVisionStudio.Application.DTOs;

public class UpdatePortfolioAlbumRequest
{
    public int PortfolioCategoryId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public int? ColumnNumber { get; set; }
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
    public string? CoverImageUrl { get; set; }
}