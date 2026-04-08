namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class GalleryUserAccessDto
{
	public string UserId { get; set; } = string.Empty;

	public string Email { get; set; } = string.Empty;

	public bool PreviewEnabled { get; set; }

	public bool DownloadEnabled { get; set; }

	public DateTime? DownloadExpiresAtUtc { get; set; }
}