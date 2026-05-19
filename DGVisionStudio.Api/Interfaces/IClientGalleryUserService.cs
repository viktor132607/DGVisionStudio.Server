using DGVisionStudio.Application.DTOs.ClientGalleries;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Application.Interfaces;

public interface IClientGalleryUserService
{
	Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId);
	Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId);
	Task<int?> CreateUserGalleryAsync(string userId, CreateUserClientGalleryRequest request);
	Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(int galleryId, string userId, IFormFile file);
	Task<bool> UserCanAccessGalleryAsync(int galleryId, string userId, bool requireDownload);
}
