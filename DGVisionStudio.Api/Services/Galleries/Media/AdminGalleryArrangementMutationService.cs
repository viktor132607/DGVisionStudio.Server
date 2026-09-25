using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryArrangementMutationService(
    IClientGalleryService clientGalleryService,
    AdminGalleryMutationAuditService audit,
    ILogger<AdminGalleryMediaMutationService> logger)
{
    public async Task<ControllerServiceResult> SetCoverAsync(
        int galleryId,
        SetGalleryCoverRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery id." });

        if (request is null)
            return ControllerServiceResult.BadRequest(
                new { message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.CoverImageUrl))
            return ControllerServiceResult.BadRequest(
                new { message = "Cover image url is required." });

        var oldGallery =
            await clientGalleryService.GetGalleryByIdAsync(galleryId);

        var updated = await clientGalleryService.SetCoverImageAsync(
            galleryId,
            request.CoverImageUrl);

        if (!updated)
            return ControllerServiceResult.NotFound(
                new { message = "Gallery or photo not found." });

        logger.LogInformation(
            "Admin changed gallery cover. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.CoverImageUrl,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "SetGalleryCover",
            "ClientGallery",
            galleryId.ToString(),
            oldGallery,
            request,
            context);

        return ControllerServiceResult.Ok(
            new { message = "Cover image updated successfully." });
    }

    public async Task<ControllerServiceResult> ReorderAsync(
        int galleryId,
        ReorderGalleryPhotosRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery id." });

        if (request is null)
            return ControllerServiceResult.BadRequest(
                new { message = "Request body is required." });

        if (request.OrderedPhotoIds is null ||
            request.OrderedPhotoIds.Count == 0)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Ordered photo ids are required." });
        }

        var oldGallery =
            await clientGalleryService.GetGalleryByIdAsync(galleryId);

        var updated = await clientGalleryService.ReorderPhotosAsync(
            galleryId,
            request.OrderedPhotoIds);

        if (!updated)
            return ControllerServiceResult.NotFound(
                new { message = "Gallery photos not found." });

        logger.LogInformation(
            "Admin reordered gallery photos. GalleryId: {GalleryId}, PhotoCount: {PhotoCount}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.OrderedPhotoIds.Count,
            context.DisplayName,
            context.TraceId);

        await audit.WriteAsync(
            "ReorderGalleryPhotos",
            "ClientGallery",
            galleryId.ToString(),
            oldGallery?.Photos.Select(photo => new
            {
                photo.Id,
                photo.DisplayOrder
            }),
            request,
            context);

        return ControllerServiceResult.Ok(
            new { message = "Photos reordered successfully." });
    }
}
