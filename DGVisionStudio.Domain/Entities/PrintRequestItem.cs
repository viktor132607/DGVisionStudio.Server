namespace DGVisionStudio.Domain.Entities;

public class PrintRequestItem
{
	public int Id { get; set; }

	public int PrintRequestId { get; set; }
	public PrintRequest? PrintRequest { get; set; }

	public int PortfolioImageId { get; set; }
	public PortfolioImage? PortfolioImage { get; set; }

	public int Quantity { get; set; } = 1;
	public string Size { get; set; } = string.Empty;
	public string? PaperType { get; set; }
	public string? Notes { get; set; }
}