namespace DGVisionStudio.Application.DTOs.ClientGalleries;

public class ReorderGalleryPhotosRequest
{
	public List<int> OrderedPhotoIds { get; set; } = new();
}