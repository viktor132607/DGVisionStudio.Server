namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class UpdateClientPhotoRequest
{
	public string? AltText { get; set; }

	public string? Caption { get; set; }

	public string? Description { get; set; }

	public int? DisplayOrder { get; set; }

	public bool? IsCover { get; set; }

	public bool? IsPublished { get; set; }

	public bool? ShowInPublicGallery { get; set; }

	public bool? VisibleToAllAuthorizedUsers { get; set; }

	public List<string>? AllowedUserIds { get; set; }
}