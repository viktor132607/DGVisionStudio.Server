using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaMutationService(
    IClientGalleryService clientGalleryService,
    IAuditLogService auditLogService,
    ILogger<AdminGalleryMediaMutationService> logger)
{
    public async Task<ControllerServiceResult> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or photo id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });

        var oldGallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var oldPhoto = oldGallery?.Photos.FirstOrDefault(x => x.Id == photoId);
        var photo = await clientGalleryService.UpdatePhotoAsync(galleryId, photoId, request);
        if (photo is null)
            return ControllerServiceResult.NotFound(new { message = "Photo not found." });

        logger.LogInformation(
            "Admin updated gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            request.IsPublished,
            request.IsCover,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "UpdateGalleryPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            oldPhoto,
            request,
            context);

        return ControllerServiceResult.Ok(photo);
    }

    public async Task<ControllerServiceResult> DeletePhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or photo id." });

        var oldGallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var oldPhoto = oldGallery?.Photos.FirstOrDefault(x => x.Id == photoId);
        var deleted = await clientGalleryService.DeletePhotoAsync(galleryId, photoId);
        if (!deleted)
            return ControllerServiceResult.NotFound(new { message = "Photo not found." });

        logger.LogWarning(
            "Admin deleted gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "DeleteGalleryPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            oldPhoto,
            null,
            context);

        return ControllerServiceResult.Ok(new { message = "Photo deleted successfully." });
    }

    public async Task<ControllerServiceResult> SetCoverImageAsync(
        int galleryId,
        SetGalleryCoverRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.CoverImageUrl))
            return ControllerServiceResult.BadRequest(new { message = "Cover image url is required." });

        var oldGallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var updated = await clientGalleryService.SetCoverImageAsync(galleryId, request.CoverImageUrl);
        if (!updated)
            return ControllerServiceResult.NotFound(new { message = "Gallery or photo not found." });

        logger.LogInformation(
            "Admin changed gallery cover. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.CoverImageUrl,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "SetGalleryCover",
            "ClientGallery",
            galleryId.ToString(),
            oldGallery,
            request,
            context);

        return ControllerServiceResult.Ok(new { message = "Cover image updated successfully." });
    }

    public async Task<ControllerServiceResult> ReorderPhotosAsync(
        int galleryId,
        ReorderGalleryPhotosRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });
        if (request.OrderedPhotoIds is null || request.OrderedPhotoIds.Count == 0)
            return ControllerServiceResult.BadRequest(new { message = "Ordered photo ids are required." });

        var oldGallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var updated = await clientGalleryService.ReorderPhotosAsync(galleryId, request.OrderedPhotoIds);
        if (!updated)
            return ControllerServiceResult.NotFound(new { message = "Gallery photos not found." });

        logger.LogInformation(
            "Admin reordered gallery photos. GalleryId: {GalleryId}, PhotoCount: {PhotoCount}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.OrderedPhotoIds.Count,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "ReorderGalleryPhotos",
            "ClientGallery",
            galleryId.ToString(),
            oldGallery?.Photos.Select(x => new { x.Id, x.DisplayOrder }),
            request,
            context);

        return ControllerServiceResult.Ok(new { message = "Photos reordered successfully." });
    }

    private Task AuditAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext context) =>
        auditLogService.LogAsync(
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