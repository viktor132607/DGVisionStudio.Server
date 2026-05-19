using DGVisionStudio.Application.DTOs.ClientGalleries;

namespace DGVisionStudio.Application.Interfaces;

public interface IClientGalleryAccessService
{
	Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId);
	Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request);
	Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request);
	Task<bool> RemoveAccessAsync(int galleryId, string userId);
	Task SyncUserAccessesAsync(int galleryId, List<GalleryUserAccessDto>? requestedAccesses);
}
