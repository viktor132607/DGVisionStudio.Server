namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class UpdateGalleryAccessRequest
{
	public bool PreviewEnabled { get; set; }

	public bool DownloadEnabled { get; set; }

	public DateTime? DownloadExpiresAtUtc { get; set; }
}