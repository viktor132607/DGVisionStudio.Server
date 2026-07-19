using System.Security.Claims;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services.Interfaces;

public interface IAdminUserService
{
    Task<ControllerServiceResult> GetUsersAsync(PagedQueryDto query);
    Task<ControllerServiceResult> MarkAllSeenAsync();
    Task<ControllerServiceResult> GetUserAlbumsAsync(string id);
    Task<ControllerServiceResult> MakeAdminAsync(string id);
    Task<ControllerServiceResult> RemoveAdminAsync(string id);
    Task<ControllerServiceResult> BlockUserAsync(string id);
    Task<ControllerServiceResult> UnblockUserAsync(string id);
    Task<ControllerServiceResult> DeleteUserAsync(string id);
}

public interface IClientGalleryEndpointService
{
    Task<ControllerServiceResult> GetMyGalleriesAsync(ClaimsPrincipal principal);
    Task<ControllerServiceResult> CreateMyGalleryAsync(ClaimsPrincipal principal, CreateUserClientGalleryRequest request);
    Task<ControllerServiceResult> GetGalleryDetailsAsync(ClaimsPrincipal principal, int galleryId);
    Task<ControllerServiceResult> UploadMyGalleryPhotoAsync(ClaimsPrincipal principal, int galleryId, IFormFile file);
    Task<ControllerServiceResult> DeleteMyGalleryAsync(ClaimsPrincipal principal, int galleryId);
    Task<ControllerServiceResult> DownloadPhotoAsync(ClaimsPrincipal principal, int galleryId, int photoId);
    Task<ControllerServiceResult> DownloadGalleryZipAsync(ClaimsPrincipal principal, int galleryId);
}

public interface IAdminCalendarService
{
    Task<ControllerServiceResult> GetAllAsync();
    Task<ControllerServiceResult> GetContactRequestsForImportAsync();
    Task<ControllerServiceResult> GetContactRequestForImportAsync(Guid id);
    Task<ControllerServiceResult> GetAsync(int id);
    Task<ControllerServiceResult> CreateAsync(CalendarEventDto dto);
    Task<ControllerServiceResult> UpdateAsync(int id, CalendarEventDto dto);
    Task<ControllerServiceResult> DeleteAsync(int id);
}

public interface IContactRequestService
{
    Task<ControllerServiceResult> CreateAsync(CreateContactRequestDto dto);
    Task<ControllerServiceResult> MarkAllSeenAsync();
    Task<ControllerServiceResult> GetAllAsync();
    Task<ControllerServiceResult> GetAsync(Guid id);
    Task<ControllerServiceResult> UpdateAsync(Guid id, UpdateContactRequestDto dto);
    Task<ControllerServiceResult> UpdateStatusAsync(Guid id, UpdateContactRequestDto dto);
    Task<ControllerServiceResult> DeleteAsync(Guid id);
}

public interface IServiceCatalogService
{
    Task<ControllerServiceResult> GetActiveAsync();
    Task<ControllerServiceResult> GetPublicByIdAsync(int id);
    Task<ControllerServiceResult> GetAllAsync();
    Task<ControllerServiceResult> GetAsync(int id);
    Task<ControllerServiceResult> CreateAsync(ServiceCardDto dto);
    Task<ControllerServiceResult> UpdateAsync(int id, ServiceCardDto dto);
    Task<ControllerServiceResult> ReorderAsync(ReorderServicesDto dto);
    Task<ControllerServiceResult> DeleteAsync(int id);
}

public interface IPortfolioQueryService
{
    Task<ControllerServiceResult> GetCategoriesAsync();
    Task<ControllerServiceResult> GetAlbumsAsync(int? categoryId);
    Task<ControllerServiceResult> GetAlbumAsync(string slug);
    Task<ControllerServiceResult> GetImagesAsync(int? albumId);
}

public interface IAdminStatisticsService
{
    Task<ControllerServiceResult> GetDashboardStatsAsync();
    Task<ControllerServiceResult> GetNotificationCountsAsync();
}

public interface ITestimonialService
{
    Task<ControllerServiceResult> GetPublishedAsync();
    Task<ControllerServiceResult> GetAllAsync();
    Task<ControllerServiceResult> CreateAsync(Testimonial entity);
    Task<ControllerServiceResult> UpdateAsync(int id, Testimonial model);
    Task<ControllerServiceResult> DeleteAsync(int id);
}

public interface IHealthService
{
    ControllerServiceResult GetHealth();
    Task<ControllerServiceResult> GetReadinessAsync();
}

public interface IPrivacyEndpointService
{
    Task<ControllerServiceResult> ExportAsync(ClaimsPrincipal principal, string traceId);
    Task<ControllerServiceResult> DeleteAccountAsync(ClaimsPrincipal principal, bool confirmed, string traceId);
}

public interface IAccountEndpointService
{
    Task<ControllerServiceResult> DeleteAccountAsync(ClaimsPrincipal principal, string password);
}

public interface IAdminAuditLogQueryService
{
    Task<ControllerServiceResult> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? entityType,
        string? entityId,
        string? adminEmail,
        string? action);

    Task<ControllerServiceResult> GetAuditLogByIdAsync(int id);
}

public interface IAdminGalleryArchiveService
{
    Task<ControllerServiceResult> CreatePhysicalArchiveAsync(CancellationToken cancellationToken);
    Task<ControllerServiceResult> PrepareStreamingArchiveAsync(CancellationToken cancellationToken);
}

public interface IAdminGalleryAccessEndpointService
{
    Task<ControllerServiceResult> GetGalleryAccessesAsync(int galleryId);
    Task<ControllerServiceResult> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request, AdminRequestContext context);
    Task<ControllerServiceResult> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request, AdminRequestContext context);
    Task<ControllerServiceResult> RemoveAccessAsync(int galleryId, string userId, AdminRequestContext context);
}

public interface IAdminGalleryMediaManagementService
{
    Task<ControllerServiceResult> UpdateMetadataAsync(int galleryId, int mediaId, UpdateGalleryMediaMetadataRequest request);
    Task<ControllerServiceResult> DownloadPhotoAsync(int galleryId, int photoId, AdminRequestContext context);
    Task<ControllerServiceResult> UploadPhotoAsync(int galleryId, IFormFile file, AdminRequestContext context);
    Task<ControllerServiceResult> UploadVideoAsync(int galleryId, IFormFile file, AdminRequestContext context);
    Task<ControllerServiceResult> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request, AdminRequestContext context);
    Task<ControllerServiceResult> DeletePhotoAsync(int galleryId, int photoId, AdminRequestContext context);
    Task<ControllerServiceResult> SetCoverImageAsync(int galleryId, SetGalleryCoverRequest request, AdminRequestContext context);
    Task<ControllerServiceResult> ReorderPhotosAsync(int galleryId, ReorderGalleryPhotosRequest request, AdminRequestContext context);
}

public interface IClientPrintRequestEndpointService
{
    Task<ControllerServiceResult> GetMineAsync(ClaimsPrincipal principal);
    Task<ControllerServiceResult> GetMineByIdAsync(ClaimsPrincipal principal, int id);
    Task<ControllerServiceResult> CreateAsync(ClaimsPrincipal principal, CreatePrintRequestDto dto);
}

public interface ICsrfTokenService
{
    string GenerateToken();
}

public interface IDebugUserService
{
    Task<ControllerServiceResult> GetUsersAsync();
}

public interface ISiteSettingsService
{
    Task<ControllerServiceResult> GetAllAsync();
}

public interface IHomeStatusService
{
    ControllerServiceResult GetStatus();
}
