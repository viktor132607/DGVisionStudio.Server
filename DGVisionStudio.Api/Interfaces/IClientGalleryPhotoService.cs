using DGVisionStudio.Application.DTOs.ClientGalleries;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Application.Interfaces;

public interface IClientGalleryPhotoService
{
	Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(int galleryId, int photoId, string userId, bool isAdmin);
	Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file);
	Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request);
	Task<bool> DeletePhotoAsync(int galleryId, int photoId);
	Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl);
	Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds);
}
