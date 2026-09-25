using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryPhotoMutationService(
    IClientGalleryService clientGalleryService,
    AdminGalleryMutationAuditService audit,
    ILogger<AdminGalleryMediaMutationService> logger)
{
    public async Task<ControllerServiceResult> UpdateAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery or photo id." });

        if (request is null)
            return ControllerServiceResult.BadRequest(
                new { message = "Request body is required." });

        var oldGallery =
            await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var oldPhoto =
            oldGallery?.Photos.FirstOrDefault(photo => photo.Id == photoId);

        var photo = await clientGalleryService.UpdatePhotoAsync(
            galleryId,
            photoId,
            request);

        if (photo is null)
            return ControllerServiceResult.NotFound(
                new { message = "Photo not found." });

        logger.LogInformation(
            "Admin updated gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            request.IsPublished,
            request.IsCover,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "UpdateGalleryPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            oldPhoto,
            request,
            context);

        return ControllerServiceResult.Ok(photo);
    }

    public async Task<ControllerServiceResult> DeleteAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery or photo id." });

        var oldGallery =
            await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var oldPhoto =
            oldGallery?.Photos.FirstOrDefault(photo => photo.Id == photoId);

        var deleted =
            await clientGalleryService.DeletePhotoAsync(
                galleryId,
                photoId);

        if (!deleted)
            return ControllerServiceResult.NotFound(
                new { message = "Photo not found." });

        logger.LogWarning(
            "Admin deleted gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "DeleteGalleryPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            oldPhoto,
            null,
            context);

        return ControllerServiceResult.Ok(
            new { message = "Photo deleted successfully." });
    }
}
