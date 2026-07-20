using System.Net;
using System.Security.Claims;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.TestSupport;

internal static class ControllerTestContext
{
    public static DefaultHttpContext Attach(ControllerBase controller, string userId = "admin-1")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, "admin@example.com"),
            new Claim(ClaimTypes.Name, "Admin User"),
            new Claim(ClaimTypes.Role, "Admin")
        ], "ControllerTests");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            TraceIdentifier = "controller-trace"
        };
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers.UserAgent = "controller-tests";
        context.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return context;
    }
}

internal sealed class StubAccountEndpointService : IAccountEndpointService
{
    public Func<ClaimsPrincipal, string, Task<ControllerServiceResult>> Handler { get; set; } =
        (_, _) => Task.FromResult(ControllerServiceResult.Ok());

    public Task<ControllerServiceResult> DeleteAccountAsync(ClaimsPrincipal principal, string password) =>
        Handler(principal, password);
}

internal sealed class StubAdminAuditLogQueryService : IAdminAuditLogQueryService
{
    public Func<int, int, string?, string?, string?, string?, Task<ControllerServiceResult>> QueryHandler { get; set; } =
        (_, _, _, _, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, Task<ControllerServiceResult>> ByIdHandler { get; set; } =
        _ => Task.FromResult(ControllerServiceResult.Ok());

    public Task<ControllerServiceResult> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? entityType,
        string? entityId,
        string? adminEmail,
        string? action) => QueryHandler(page, pageSize, entityType, entityId, adminEmail, action);

    public Task<ControllerServiceResult> GetAuditLogByIdAsync(int id) => ByIdHandler(id);
}

internal sealed class StubAdminCalendarService : IAdminCalendarService
{
    public Func<Task<ControllerServiceResult>> GetAllHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Task<ControllerServiceResult>> ContactRequestsHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Guid, Task<ControllerServiceResult>> ContactRequestHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, Task<ControllerServiceResult>> GetHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<CalendarEventDto, Task<ControllerServiceResult>> CreateHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, CalendarEventDto, Task<ControllerServiceResult>> UpdateHandler { get; set; } = (_, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, Task<ControllerServiceResult>> DeleteHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.NoContent());

    public Task<ControllerServiceResult> GetAllAsync() => GetAllHandler();
    public Task<ControllerServiceResult> GetContactRequestsForImportAsync() => ContactRequestsHandler();
    public Task<ControllerServiceResult> GetContactRequestForImportAsync(Guid id) => ContactRequestHandler(id);
    public Task<ControllerServiceResult> GetAsync(int id) => GetHandler(id);
    public Task<ControllerServiceResult> CreateAsync(CalendarEventDto dto) => CreateHandler(dto);
    public Task<ControllerServiceResult> UpdateAsync(int id, CalendarEventDto dto) => UpdateHandler(id, dto);
    public Task<ControllerServiceResult> DeleteAsync(int id) => DeleteHandler(id);
}

internal sealed class StubAdminGalleryArchiveService : IAdminGalleryArchiveService
{
    public Func<CancellationToken, Task<ControllerServiceResult>> PhysicalHandler { get; set; } =
        _ => Task.FromResult(ControllerServiceResult.NotFound());
    public Func<CancellationToken, Task<ControllerServiceResult>> StreamingHandler { get; set; } =
        _ => Task.FromResult(ControllerServiceResult.NotFound());

    public Task<ControllerServiceResult> CreatePhysicalArchiveAsync(CancellationToken cancellationToken) =>
        PhysicalHandler(cancellationToken);

    public Task<ControllerServiceResult> PrepareStreamingArchiveAsync(CancellationToken cancellationToken) =>
        StreamingHandler(cancellationToken);
}

internal sealed class StubAdminGalleryAccessEndpointService : IAdminGalleryAccessEndpointService
{
    public Func<int, Task<ControllerServiceResult>> GetHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, GrantGalleryAccessRequest, AdminRequestContext, Task<ControllerServiceResult>> GrantHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, string, UpdateGalleryAccessRequest, AdminRequestContext, Task<ControllerServiceResult>> UpdateHandler { get; set; } =
        (_, _, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, string, AdminRequestContext, Task<ControllerServiceResult>> RemoveHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.NoContent());

    public Task<ControllerServiceResult> GetGalleryAccessesAsync(int galleryId) => GetHandler(galleryId);
    public Task<ControllerServiceResult> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request, AdminRequestContext context) => GrantHandler(galleryId, request, context);
    public Task<ControllerServiceResult> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request, AdminRequestContext context) => UpdateHandler(galleryId, userId, request, context);
    public Task<ControllerServiceResult> RemoveAccessAsync(int galleryId, string userId, AdminRequestContext context) => RemoveHandler(galleryId, userId, context);
}

internal sealed class StubAdminGalleryMediaManagementService : IAdminGalleryMediaManagementService
{
    public Func<int, int, UpdateGalleryMediaMetadataRequest, Task<ControllerServiceResult>> MetadataHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, int, AdminRequestContext, Task<ControllerServiceResult>> DownloadHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.NotFound());
    public Func<int, IFormFile, AdminRequestContext, Task<ControllerServiceResult>> UploadPhotoHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, IFormFile, AdminRequestContext, Task<ControllerServiceResult>> UploadVideoHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, int, UpdateClientPhotoRequest, AdminRequestContext, Task<ControllerServiceResult>> UpdatePhotoHandler { get; set; } =
        (_, _, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, int, AdminRequestContext, Task<ControllerServiceResult>> DeletePhotoHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.NoContent());
    public Func<int, SetGalleryCoverRequest, AdminRequestContext, Task<ControllerServiceResult>> CoverHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, ReorderGalleryPhotosRequest, AdminRequestContext, Task<ControllerServiceResult>> ReorderHandler { get; set; } =
        (_, _, _) => Task.FromResult(ControllerServiceResult.Ok());

    public Task<ControllerServiceResult> UpdateMetadataAsync(int galleryId, int mediaId, UpdateGalleryMediaMetadataRequest request) => MetadataHandler(galleryId, mediaId, request);
    public Task<ControllerServiceResult> DownloadPhotoAsync(int galleryId, int photoId, AdminRequestContext context) => DownloadHandler(galleryId, photoId, context);
    public Task<ControllerServiceResult> UploadPhotoAsync(int galleryId, IFormFile file, AdminRequestContext context) => UploadPhotoHandler(galleryId, file, context);
    public Task<ControllerServiceResult> UploadVideoAsync(int galleryId, IFormFile file, AdminRequestContext context) => UploadVideoHandler(galleryId, file, context);
    public Task<ControllerServiceResult> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request, AdminRequestContext context) => UpdatePhotoHandler(galleryId, photoId, request, context);
    public Task<ControllerServiceResult> DeletePhotoAsync(int galleryId, int photoId, AdminRequestContext context) => DeletePhotoHandler(galleryId, photoId, context);
    public Task<ControllerServiceResult> SetCoverImageAsync(int galleryId, SetGalleryCoverRequest request, AdminRequestContext context) => CoverHandler(galleryId, request, context);
    public Task<ControllerServiceResult> ReorderPhotosAsync(int galleryId, ReorderGalleryPhotosRequest request, AdminRequestContext context) => ReorderHandler(galleryId, request, context);
}

internal sealed class StubContactRequestService : IContactRequestService
{
    public Func<CreateContactRequestDto, Task<ControllerServiceResult>> CreateHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Task<ControllerServiceResult>> MarkAllSeenHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.NoContent());
    public Func<Task<ControllerServiceResult>> GetAllHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Guid, Task<ControllerServiceResult>> GetHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Guid, UpdateContactRequestDto, Task<ControllerServiceResult>> UpdateHandler { get; set; } = (_, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Guid, UpdateContactRequestDto, Task<ControllerServiceResult>> StatusHandler { get; set; } = (_, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Guid, Task<ControllerServiceResult>> DeleteHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.NoContent());

    public Task<ControllerServiceResult> CreateAsync(CreateContactRequestDto dto) => CreateHandler(dto);
    public Task<ControllerServiceResult> MarkAllSeenAsync() => MarkAllSeenHandler();
    public Task<ControllerServiceResult> GetAllAsync() => GetAllHandler();
    public Task<ControllerServiceResult> GetAsync(Guid id) => GetHandler(id);
    public Task<ControllerServiceResult> UpdateAsync(Guid id, UpdateContactRequestDto dto) => UpdateHandler(id, dto);
    public Task<ControllerServiceResult> UpdateStatusAsync(Guid id, UpdateContactRequestDto dto) => StatusHandler(id, dto);
    public Task<ControllerServiceResult> DeleteAsync(Guid id) => DeleteHandler(id);
}

internal sealed class StubAdminStatisticsService : IAdminStatisticsService
{
    public Func<Task<ControllerServiceResult>> DashboardHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.Ok());
    public Func<Task<ControllerServiceResult>> NotificationsHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.Ok());
    public Task<ControllerServiceResult> GetDashboardStatsAsync() => DashboardHandler();
    public Task<ControllerServiceResult> GetNotificationCountsAsync() => NotificationsHandler();
}

internal sealed class StubPricingService : IPricingService
{
    public Func<Task<IReadOnlyList<PricingItemResponse>>> ActiveHandler { get; set; } = () => Task.FromResult<IReadOnlyList<PricingItemResponse>>([]);
    public Func<Task<IReadOnlyList<PricingItemResponse>>> AllHandler { get; set; } = () => Task.FromResult<IReadOnlyList<PricingItemResponse>>([]);
    public Func<PricingItemRequest, Task<PricingItemResponse>> CreateHandler { get; set; } = _ => throw new NotImplementedException();
    public Func<int, PricingItemRequest, Task<PricingItemResponse?>> UpdateHandler { get; set; } = (_, _) => Task.FromResult<PricingItemResponse?>(null);
    public Func<ReorderPricingItemsRequest, Task<IReadOnlyList<PricingItemResponse>>> ReorderHandler { get; set; } = _ => Task.FromResult<IReadOnlyList<PricingItemResponse>>([]);
    public Func<int, Task<bool>> DeleteHandler { get; set; } = _ => Task.FromResult(false);

    public Task<IReadOnlyList<PricingItemResponse>> GetActiveAsync() => ActiveHandler();
    public Task<IReadOnlyList<PricingItemResponse>> GetAllAsync() => AllHandler();
    public Task<PricingItemResponse> CreateAsync(PricingItemRequest request) => CreateHandler(request);
    public Task<PricingItemResponse?> UpdateAsync(int id, PricingItemRequest request) => UpdateHandler(id, request);
    public Task<IReadOnlyList<PricingItemResponse>> ReorderAsync(ReorderPricingItemsRequest request) => ReorderHandler(request);
    public Task<bool> DeleteAsync(int id) => DeleteHandler(id);
}

internal sealed class StubAdminPrintRequestService : IAdminPrintRequestService
{
    public Func<Task<ControllerServiceResult>> AllHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, Task<ControllerServiceResult>> ByIdHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, UpdatePrintRequestStatusDto, Task<ControllerServiceResult>> StatusHandler { get; set; } = (_, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<int, Task<ControllerServiceResult>> SeenHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.NoContent());
    public Func<Task<ControllerServiceResult>> AllSeenHandler { get; set; } = () => Task.FromResult(ControllerServiceResult.NoContent());
    public Func<int, Task<ControllerServiceResult>> DeleteHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.NoContent());

    public Task<ControllerServiceResult> GetAllAsync() => AllHandler();
    public Task<ControllerServiceResult> GetByIdAsync(int id) => ByIdHandler(id);
    public Task<ControllerServiceResult> UpdateStatusAsync(int id, UpdatePrintRequestStatusDto dto) => StatusHandler(id, dto);
    public Task<ControllerServiceResult> MarkSeenAsync(int id) => SeenHandler(id);
    public Task<ControllerServiceResult> MarkAllSeenAsync() => AllSeenHandler();
    public Task<ControllerServiceResult> DeleteAsync(int id) => DeleteHandler(id);
}

internal sealed class StubClientPrintRequestEndpointService : IClientPrintRequestEndpointService
{
    public Func<ClaimsPrincipal, Task<ControllerServiceResult>> MineHandler { get; set; } = _ => Task.FromResult(ControllerServiceResult.Ok());
    public Func<ClaimsPrincipal, int, Task<ControllerServiceResult>> ByIdHandler { get; set; } = (_, _) => Task.FromResult(ControllerServiceResult.Ok());
    public Func<ClaimsPrincipal, CreatePrintRequestDto, Task<ControllerServiceResult>> CreateHandler { get; set; } = (_, _) => Task.FromResult(ControllerServiceResult.Ok());

    public Task<ControllerServiceResult> GetMineAsync(ClaimsPrincipal principal) => MineHandler(principal);
    public Task<ControllerServiceResult> GetMineByIdAsync(ClaimsPrincipal principal, int id) => ByIdHandler(principal, id);
    public Task<ControllerServiceResult> CreateAsync(ClaimsPrincipal principal, CreatePrintRequestDto dto) => CreateHandler(principal, dto);
}

internal sealed class StubCsrfTokenService(string token = "csrf-token") : ICsrfTokenService
{
    public string GenerateToken() => token;
}

internal sealed class StubDebugUserService : IDebugUserService
{
    public ControllerServiceResult Result { get; set; } = ControllerServiceResult.Ok();
    public Task<ControllerServiceResult> GetUsersAsync() => Task.FromResult(Result);
}

internal sealed class StubHomeStatusService : IHomeStatusService
{
    public ControllerServiceResult Result { get; set; } = ControllerServiceResult.Ok();
    public ControllerServiceResult GetStatus() => Result;
}

internal sealed class StubSiteSettingsService : ISiteSettingsService
{
    public ControllerServiceResult Result { get; set; } = ControllerServiceResult.Ok();
    public Task<ControllerServiceResult> GetAllAsync() => Task.FromResult(Result);
}
