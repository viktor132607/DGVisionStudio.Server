namespace DGVisionStudio.Domain.Entities;

public class UserAlbumAccess
{
	public int Id { get; set; }

	public int PortfolioAlbumId { get; set; }
	public PortfolioAlbum PortfolioAlbum { get; set; } = null!;

	public string UserId { get; set; } = string.Empty;
	public ApplicationUser User { get; set; } = null!;

	public bool PreviewEnabled { get; set; } = true;
	public bool DownloadEnabled { get; set; } = false;
	public DateTime? DownloadExpiresAtUtc { get; set; }
}