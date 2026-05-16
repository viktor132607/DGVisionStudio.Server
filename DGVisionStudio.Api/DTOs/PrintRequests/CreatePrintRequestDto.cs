namespace DGVisionStudio.Application.DTOs.PrintRequests;

public class CreatePrintRequestDto
{
	public int PortfolioAlbumId { get; set; }
	public string FullName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? Phone { get; set; }
	public string? Notes { get; set; }
	public List<CreatePrintRequestItemDto> Items { get; set; } = new();
}