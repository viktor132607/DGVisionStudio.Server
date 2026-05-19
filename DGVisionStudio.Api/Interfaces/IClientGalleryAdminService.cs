using DGVisionStudio.Application.DTOs.ClientGalleries;

namespace DGVisionStudio.Application.Interfaces;

public interface IClientGalleryAdminService
{
	Task<List<MyClientGalleryDto>> GetAllGalleriesAsync();
	Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId);
	Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request);
	Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request);
	Task<bool> DeleteGalleryAsync(int galleryId);
}
