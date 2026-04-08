using DGVisionStudio.Application.DTOs.ClientGalleries;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Application.Interfaces;

public interface IClientGalleryService
{
	Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId);
	Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId);

	Task<List<MyClientGalleryDto>> GetAllGalleriesAsync();
	Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId);

	Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request);
	Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request);
	Task<bool> DeleteGalleryAsync(int galleryId);

	Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId);
	Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request);
	Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request);
	Task<bool> RemoveAccessAsync(int galleryId, string userId);

	Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file);
	Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request);
	Task<bool> DeletePhotoAsync(int galleryId, int photoId);
	Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl);
	Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds);
}