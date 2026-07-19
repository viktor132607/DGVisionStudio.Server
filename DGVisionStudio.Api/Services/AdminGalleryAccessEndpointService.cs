using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryAccessEndpointService : IAdminGalleryAccessEndpointService
{
    private readonly IClientGalleryService _clientGalleryService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminGalleryAccessEndpointService> _logger;

    public AdminGalleryAccessEndpointService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminGalleryAccessEndpointService> logger)
    {
        _clientGalleryService = clientGalleryService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ControllerServiceResult> GetGalleryAccessesAsync(int galleryId)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });

        var gallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
        if (gallery is null)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        return ControllerServiceResult.Ok(
            await _clientGalleryService.GetGalleryAccessesAsync(galleryId));
    }

    public async Task<ControllerServiceResult> GrantAccessAsync(
        int galleryId,
        GrantGalleryAccessRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.UserEmail))
            return ControllerServiceResult.BadRequest(new { message = "User email is required." });

        var granted = await _clientGalleryService.GrantAccessAsync(galleryId, request);
        if (!granted)
            return ControllerServiceResult.BadRequest(new { message = "Gallery or user was not found." });

        _logger.LogInformation(
            "Admin granted gallery access. GalleryId: {GalleryId}, UserEmail: {UserEmail}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.UserEmail,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "GrantGalleryAccess",
            "ClientGalleryAccess",
            galleryId.ToString(),
            null,
            request,
            context);

        return ControllerServiceResult.Ok(new { message = "Gallery access updated successfully." });
    }

    public async Task<ControllerServiceResult> UpdateAccessAsync(
        int galleryId,
        string userId,
        UpdateGalleryAccessRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.BadRequest(new { message = "User id is required." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });

        var oldAccesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
        var oldAccess = oldAccesses.FirstOrDefault(x => x.UserId == userId);
        var updated = await _clientGalleryService.UpdateAccessAsync(galleryId, userId, request);
        if (!updated)
            return ControllerServiceResult.NotFound(new { message = "Gallery access not found." });

        _logger.LogInformation(
            "Admin updated gallery access. GalleryId: {GalleryId}, UserId: {UserId}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            userId,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "UpdateGalleryAccess",
            "ClientGalleryAccess",
            $"{galleryId}:{userId}",
            oldAccess,
            request,
            context);

        return ControllerServiceResult.Ok(new { message = "Gallery access updated successfully." });
    }

    public async Task<ControllerServiceResult> RemoveAccessAsync(
        int galleryId,
        string userId,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.BadRequest(new { message = "User id is required." });

        var oldAccesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
        var oldAccess = oldAccesses.FirstOrDefault(x => x.UserId == userId);
        var removed = await _clientGalleryService.RemoveAccessAsync(galleryId, userId);
        if (!removed)
            return ControllerServiceResult.NotFound(new { message = "Gallery access not found." });

        _logger.LogWarning(
            "Admin removed gallery access. GalleryId: {GalleryId}, UserId: {UserId}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            userId,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "RemoveGalleryAccess",
            "ClientGalleryAccess",
            $"{galleryId}:{userId}",
            oldAccess,
            null,
            context);

        return ControllerServiceResult.Ok(new { message = "Gallery access removed successfully." });
    }

    private Task AuditAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext context) =>
        _auditLogService.LogAsync(
            context.UserId,
            context.Email,
            action,
            entityType,
            entityId,
            oldValue,
            newValue,
            context.RemoteIpAddress,
            context.UserAgent,
            context.TraceId);
}
