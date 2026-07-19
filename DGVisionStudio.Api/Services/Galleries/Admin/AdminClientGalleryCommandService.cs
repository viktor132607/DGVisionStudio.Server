using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminClientGalleryCommandService(
    IClientGalleryService clientGalleryService,
    IAuditLogService auditLogService,
    ILogger<AdminClientGalleryCommandService> logger)
{
    public async Task<ControllerServiceResult> CreateGalleryAsync(
        AdminCreateClientGalleryRequest? request,
        AdminRequestContext context)
    {
        if (request == null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.Title))
            return ControllerServiceResult.BadRequest(new { message = "Title is required." });

        var galleryId = await clientGalleryService.CreateGalleryAsync(request);
        logger.LogInformation(
            "Admin created client gallery. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, UserGalleryStatus: {UserGalleryStatus}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.Title,
            request.GalleryType,
            request.UserGalleryStatus,
            request.IsPublic,
            request.IsPublished,
            context.DisplayName,
            context.TraceId);

        await AuditAsync("CreateGallery", "ClientGallery", galleryId.ToString(), null, request, context);
        return ControllerServiceResult.Ok(new
        {
            message = "Client gallery created successfully.",
            id = galleryId
        });
    }

    public async Task<ControllerServiceResult> UpdateGalleryAsync(
        int galleryId,
        AdminUpdateClientGalleryRequest? request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });

        if (request == null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.Title))
            return ControllerServiceResult.BadRequest(new { message = "Title is required." });

        var oldGallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var updated = await clientGalleryService.UpdateGalleryAsync(galleryId, request);
        if (!updated)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        logger.LogInformation(
            "Admin updated client gallery. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, UserGalleryStatus: {UserGalleryStatus}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.Title,
            request.GalleryType,
            request.UserGalleryStatus,
            request.IsPublic,
            request.IsPublished,
            context.DisplayName,
            context.TraceId);

        await AuditAsync("UpdateGallery", "ClientGallery", galleryId.ToString(), oldGallery, request, context);
        return ControllerServiceResult.Ok(new { message = "Client gallery updated successfully." });
    }

    public async Task<ControllerServiceResult> DeleteGalleryAsync(
        int galleryId,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });

        var oldGallery = await clientGalleryService.GetGalleryByIdAsync(galleryId);
        var deleted = await clientGalleryService.DeleteGalleryAsync(galleryId);
        if (!deleted)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        logger.LogWarning(
            "Admin deleted client gallery. GalleryId: {GalleryId}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            context.DisplayName,
            context.TraceId);

        await AuditAsync("DeleteGallery", "ClientGallery", galleryId.ToString(), oldGallery, null, context);
        return ControllerServiceResult.Ok(new { message = "Client gallery deleted successfully." });
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