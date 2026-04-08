namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class GrantGalleryAccessRequest
{
	public string UserEmail { get; set; } = string.Empty;

	public bool PreviewEnabled { get; set; } = true;

	public bool DownloadEnabled { get; set; } = false;

	public DateTime? DownloadExpiresAtUtc { get; set; }
}