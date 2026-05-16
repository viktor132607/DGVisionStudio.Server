namespace DGVisionStudio.Application.DTOs.PrintRequests;

public class CreatePrintRequestItemDto
{
	public int PortfolioImageId { get; set; }
	public int Quantity { get; set; } = 1;
	public string Size { get; set; } = string.Empty;
	public string? PaperType { get; set; }
	public string? Notes { get; set; }
}