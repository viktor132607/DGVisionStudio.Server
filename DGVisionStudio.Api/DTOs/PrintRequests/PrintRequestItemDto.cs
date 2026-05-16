namespace DGVisionStudio.Application.DTOs.PrintRequests;

public class PrintRequestItemDto
{
	public int Id { get; set; }
	public int PortfolioImageId { get; set; }
	public string ImageUrl { get; set; } = string.Empty;
	public string? ThumbnailUrl { get; set; }
	public int Quantity { get; set; }
	public string Size { get; set; } = string.Empty;
	public string? PaperType { get; set; }
	public string? Notes { get; set; }
}