using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaDownloadService(
    IClientGalleryService clientGalleryService,
    IAuditLogService auditLogService,
    ILogger<AdminGalleryMediaDownloadService> logger)
{
    public async Task<ControllerServiceResult> DownloadPhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or photo id." });

        var result = await clientGalleryService.OpenPhotoDownloadAsync(
            galleryId,
            photoId,
            userId: string.Empty,
            isAdmin: true);

        if (result is null)
        {
            logger.LogWarning(
                "Admin photo download failed. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
                galleryId,
                photoId,
                context.DisplayName,
                context.TraceId);
            return ControllerServiceResult.NotFound(new { message = "Photo not found." });
        }

        logger.LogInformation(
            "Admin downloaded photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            result.Value.FileName,
            context.DisplayName,
            context.TraceId);

        await auditLogService.LogAsync(
            context.UserId,
            context.Email,
            "DownloadPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            null,
            new { GalleryId = galleryId, PhotoId = photoId, result.Value.FileName },
            context.RemoteIpAddress,
            context.UserAgent,
            context.TraceId);

        return ControllerServiceResult.Ok(new FileDownloadResult(
            result.Value.Stream,
            result.Value.ContentType,
            result.Value.FileName));
    }
}