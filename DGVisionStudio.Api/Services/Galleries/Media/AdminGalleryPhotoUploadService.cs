using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryPhotoUploadService(
    IClientGalleryService clientGalleryService,
    IAuditLogService auditLogService,
    ILogger<AdminGalleryMediaUploadService> logger)
{
    private const long MaxPhotoUploadSizeBytes = 20 * 1024 * 1024;

    public async Task<ControllerServiceResult> UploadAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });

        if (file is null || file.Length == 0)
            return ControllerServiceResult.BadRequest(new { message = "File is required." });

        if (file.Length > MaxPhotoUploadSizeBytes)
            return ControllerServiceResult.BadRequest(new { message = "Photo is too large. Maximum size is 20MB." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return ControllerServiceResult.BadRequest(new { message = "Only image files are allowed." });

        var photo = await clientGalleryService.UploadPhotoAsync(galleryId, file);
        if (photo is null)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        logger.LogInformation(
            "Admin uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photo.Id,
            file.FileName,
            file.Length,
            file.ContentType,
            context.DisplayName,
            context.TraceId);

        await auditLogService.LogAsync(
            context.UserId,
            context.Email,
            "UploadGalleryPhoto",
            "ClientGalleryPhoto",
            photo.Id.ToString(),
            null,
            new
            {
                GalleryId = galleryId,
                PhotoId = photo.Id,
                file.FileName,
                file.Length,
                file.ContentType
            },
            context.RemoteIpAddress,
            context.UserAgent,
            context.TraceId);

        return ControllerServiceResult.Ok(photo);
    }
}
