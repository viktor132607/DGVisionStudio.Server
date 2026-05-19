using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Infrastructure.Services;

public class ClientGalleryService : IClientGalleryService
{
	private readonly IClientGalleryAdminService _adminService;
	private readonly IClientGalleryUserService _userService;
	private readonly IClientGalleryAccessService _accessService;
	private readonly IClientGalleryPhotoService _photoService;
	private readonly IClientGalleryExpiryService _expiryService;

	public ClientGalleryService(
		IClientGalleryAdminService adminService,
		IClientGalleryUserService userService,
		IClientGalleryAccessService accessService,
		IClientGalleryPhotoService photoService,
		IClientGalleryExpiryService expiryService)
	{
		_adminService = adminService;
		_userService = userService;
		_accessService = accessService;
		_photoService = photoService;
		_expiryService = expiryService;
	}

	public Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId) => _userService.GetMyGalleriesAsync(userId);
	public Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId) => _userService.GetGalleryDetailsAsync(galleryId, userId);
	public Task<List<MyClientGalleryDto>> GetAllGalleriesAsync() => _adminService.GetAllGalleriesAsync();
	public Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId) => _adminService.GetGalleryByIdAsync(galleryId);
	public Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request) => _adminService.CreateGalleryAsync(request);
	public Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request) => _adminService.UpdateGalleryAsync(galleryId, request);
	public Task<bool> DeleteGalleryAsync(int galleryId) => _adminService.DeleteGalleryAsync(galleryId);
	public Task<int?> CreateUserGalleryAsync(string userId, CreateUserClientGalleryRequest request) => _userService.CreateUserGalleryAsync(userId, request);
	public Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(int galleryId, string userId, IFormFile file) => _userService.UploadUserGalleryPhotoAsync(galleryId, userId, file);
	public Task<bool> UserCanAccessGalleryAsync(int galleryId, string userId, bool requireDownload) => _userService.UserCanAccessGalleryAsync(galleryId, userId, requireDownload);
	public Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(int galleryId, int photoId, string userId, bool isAdmin) => _photoService.OpenPhotoDownloadAsync(galleryId, photoId, userId, isAdmin);
	public Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId) => _accessService.GetGalleryAccessesAsync(galleryId);
	public Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request) => _accessService.GrantAccessAsync(galleryId, request);
	public Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request) => _accessService.UpdateAccessAsync(galleryId, userId, request);
	public Task<bool> RemoveAccessAsync(int galleryId, string userId) => _accessService.RemoveAccessAsync(galleryId, userId);
	public Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file) => _photoService.UploadPhotoAsync(galleryId, file);
	public Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request) => _photoService.UpdatePhotoAsync(galleryId, photoId, request);
	public Task<bool> DeletePhotoAsync(int galleryId, int photoId) => _photoService.DeletePhotoAsync(galleryId, photoId);
	public Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl) => _photoService.SetCoverImageAsync(galleryId, coverImageUrl);
	public Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds) => _photoService.ReorderPhotosAsync(galleryId, orderedPhotoIds);
	public Task<int> MarkExpiredUserGalleriesAsync(CancellationToken cancellationToken = default) => _expiryService.MarkExpiredUserGalleriesAsync(cancellationToken);
	public Task<int> DeleteExpiredUserGalleriesAsync(CancellationToken cancellationToken = default) => _expiryService.DeleteExpiredUserGalleriesAsync(cancellationToken);
}
