using DGVisionStudio.Application.DTOs.ClientGalleries;

namespace DGVisionStudio.Api.Services.Interfaces;

public interface IAdminClientGalleryManagementService
{
    Task<ControllerServiceResult> GetAllGalleriesAsync();
    Task<ControllerServiceResult> DownloadAllAlbumsAsync(AdminRequestContext context);
    Task<ControllerServiceResult> GetAvailableUsersAsync();
    Task<ControllerServiceResult> GetGalleryByIdAsync(int galleryId);
    Task<ControllerServiceResult> CreateGalleryAsync(AdminCreateClientGalleryRequest? request, AdminRequestContext context);
    Task<ControllerServiceResult> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest? request, AdminRequestContext context);
    Task<ControllerServiceResult> DeleteGalleryAsync(int galleryId, AdminRequestContext context);
}
