namespace DGVisionStudio.Application.DTOs.PrintRequests;

public class PrintRequestDto
{
	public int Id { get; set; }

	public string UserId { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;

	public int PortfolioAlbumId { get; set; }
	public string AlbumTitle { get; set; } = string.Empty;

	public string FullName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? Phone { get; set; }
	public string? Notes { get; set; }

	public string Status { get; set; } = string.Empty;
	public bool IsSeenByAdmin { get; set; }

	public DateTime CreatedAtUtc { get; set; }
	public DateTime? UpdatedAtUtc { get; set; }

	public List<PrintRequestItemDto> Items { get; set; } = new();
}