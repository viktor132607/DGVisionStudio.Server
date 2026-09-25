using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaMutationService
{
    private readonly AdminGalleryPhotoMutationService _photos;
    private readonly AdminGalleryArrangementMutationService _arrangement;

    [ActivatorUtilitiesConstructor]
    public AdminGalleryMediaMutationService(
        AdminGalleryPhotoMutationService photos,
        AdminGalleryArrangementMutationService arrangement)
    {
        _photos = photos;
        _arrangement = arrangement;
    }

    public AdminGalleryMediaMutationService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminGalleryMediaMutationService> logger)
        : this(
            new AdminGalleryPhotoMutationService(
                clientGalleryService,
                new AdminGalleryMutationAuditService(auditLogService),
                logger),
            new AdminGalleryArrangementMutationService(
                clientGalleryService,
                new AdminGalleryMutationAuditService(auditLogService),
                logger))
    {
    }

    public Task<ControllerServiceResult> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request,
        AdminRequestContext context) =>
        _photos.UpdateAsync(
            galleryId,
            photoId,
            request,
            context);

    public Task<ControllerServiceResult> DeletePhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context) =>
        _photos.DeleteAsync(
            galleryId,
            photoId,
            context);

    public Task<ControllerServiceResult> SetCoverImageAsync(
        int galleryId,
        SetGalleryCoverRequest request,
        AdminRequestContext context) =>
        _arrangement.SetCoverAsync(
            galleryId,
            request,
            context);

    public Task<ControllerServiceResult> ReorderPhotosAsync(
        int galleryId,
        ReorderGalleryPhotosRequest request,
        AdminRequestContext context) =>
        _arrangement.ReorderAsync(
            galleryId,
            request,
            context);
}
