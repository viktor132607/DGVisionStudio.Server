using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryAccessMutationService(
    IClientGalleryService clientGalleryService,
    AdminGalleryMutationAuditService audit,
    ILogger<AdminGalleryAccessEndpointService> logger)
{
    public async Task<ControllerServiceResult> GrantAsync(
        int galleryId,
        GrantGalleryAccessRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery id." });
        }

        if (request is null)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.UserEmail))
        {
            return ControllerServiceResult.BadRequest(
                new { message = "User email is required." });
        }

        var granted =
            await clientGalleryService.GrantAccessAsync(
                galleryId,
                request);

        if (!granted)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Gallery or user was not found." });
        }

        logger.LogInformation(
            "Admin granted gallery access. GalleryId: {GalleryId}, UserEmail: {UserEmail}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.UserEmail,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "GrantGalleryAccess",
            "ClientGalleryAccess",
            galleryId.ToString(),
            null,
            request,
            context);

        return ControllerServiceResult.Ok(
            new { message = "Gallery access updated successfully." });
    }

    public async Task<ControllerServiceResult> UpdateAsync(
        int galleryId,
        string userId,
        UpdateGalleryAccessRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery id." });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return ControllerServiceResult.BadRequest(
                new { message = "User id is required." });
        }

        if (request is null)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Request body is required." });
        }

        var oldAccesses =
            await clientGalleryService.GetGalleryAccessesAsync(galleryId);
        var oldAccess =
            oldAccesses.FirstOrDefault(access => access.UserId == userId);

        var updated =
            await clientGalleryService.UpdateAccessAsync(
                galleryId,
                userId,
                request);

        if (!updated)
        {
            return ControllerServiceResult.NotFound(
                new { message = "Gallery access not found." });
        }

        logger.LogInformation(
            "Admin updated gallery access. GalleryId: {GalleryId}, UserId: {UserId}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            userId,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "UpdateGalleryAccess",
            "ClientGalleryAccess",
            $"{galleryId}:{userId}",
            oldAccess,
            request,
            context);

        return ControllerServiceResult.Ok(
            new { message = "Gallery access updated successfully." });
    }

    public async Task<ControllerServiceResult> RemoveAsync(
        int galleryId,
        string userId,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery id." });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return ControllerServiceResult.BadRequest(
                new { message = "User id is required." });
        }

        var oldAccesses =
            await clientGalleryService.GetGalleryAccessesAsync(galleryId);
        var oldAccess =
            oldAccesses.FirstOrDefault(access => access.UserId == userId);

        var removed =
            await clientGalleryService.RemoveAccessAsync(
                galleryId,
                userId);

        if (!removed)
        {
            return ControllerServiceResult.NotFound(
                new { message = "Gallery access not found." });
        }

        logger.LogWarning(
            "Admin removed gallery access. GalleryId: {GalleryId}, UserId: {UserId}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            userId,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "RemoveGalleryAccess",
            "ClientGalleryAccess",
            $"{galleryId}:{userId}",
            oldAccess,
            null,
            context);

        return ControllerServiceResult.Ok(
            new { message = "Gallery access removed successfully." });
    }
}
