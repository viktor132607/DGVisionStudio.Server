namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class ClientPhotoDto
{
	public int Id { get; set; }

	public string PreviewUrl { get; set; } = string.Empty;

	public string? OriginalUrl { get; set; }

	public string? AltText { get; set; }

	public string? Caption { get; set; }

	public bool CanDownload { get; set; }

	public int DisplayOrder { get; set; }

	public string? Description { get; set; }

	public bool IsPublished { get; set; }

	public bool ShowInPublicGallery { get; set; }

	public bool VisibleToAllAuthorizedUsers { get; set; }

	public List<string> AllowedUserIds { get; set; } = new();
}