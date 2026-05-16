namespace DGVisionStudio.Domain.Entities;

public class PrintRequest
{
	public int Id { get; set; }

	public string UserId { get; set; } = string.Empty;
	public ApplicationUser? User { get; set; }

	public int PortfolioAlbumId { get; set; }
	public PortfolioAlbum? PortfolioAlbum { get; set; }

	public string FullName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? Phone { get; set; }
	public string? Notes { get; set; }

	public string Status { get; set; } = "New";
	public bool IsSeenByAdmin { get; set; }

	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAtUtc { get; set; }

	public ICollection<PrintRequestItem> Items { get; set; } = new List<PrintRequestItem>();
}