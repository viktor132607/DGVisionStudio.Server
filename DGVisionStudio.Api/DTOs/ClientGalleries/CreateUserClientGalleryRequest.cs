namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class CreateUserClientGalleryRequest
{
	public string Title { get; set; } = string.Empty;

	public string? Description { get; set; }
}