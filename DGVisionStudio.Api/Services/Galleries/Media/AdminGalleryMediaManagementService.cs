using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaManagementService : IAdminGalleryMediaManagementService
{
    private readonly AdminGalleryMediaMetadataService _metadata;
    private readonly AdminGalleryMediaDownloadService _downloads;
    private readonly AdminGalleryMediaUploadService _uploads;
    private readonly AdminGalleryMediaMutationService _mutations;

    [ActivatorUtilitiesConstructor]
    public AdminGalleryMediaManagementService(
        AdminGalleryMediaMetadataService metadata,
        AdminGalleryMediaDownloadService downloads,
        AdminGalleryMediaUploadService uploads,
        AdminGalleryMediaMutationService mutations)
    {
        _metadata = metadata;
        _downloads = downloads;
        _uploads = uploads;
        _mutations = mutations;
    }

    public AdminGalleryMediaManagementService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminGalleryMediaManagementService> logger,
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        ClientGalleryMapper mapper)
        : this(
            new AdminGalleryMediaMetadataService(dbContext),
            new AdminGalleryMediaDownloadService(
                clientGalleryService,
                auditLogService,
                NullLogger<AdminGalleryMediaDownloadService>.Instance),
            new AdminGalleryMediaUploadService(
                clientGalleryService,
                auditLogService,
                NullLogger<AdminGalleryMediaUploadService>.Instance,
                dbContext,
                environment,
                mapper),
            new AdminGalleryMediaMutationService(
                clientGalleryService,
                auditLogService,
                NullLogger<AdminGalleryMediaMutationService>.Instance))
    {
    }

    public Task<ControllerServiceResult> UpdateMetadataAsync(
        int galleryId,
        int mediaId,
        UpdateGalleryMediaMetadataRequest request) =>
        _metadata.UpdateMetadataAsync(galleryId, mediaId, request);

    public Task<ControllerServiceResult> DownloadPhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context) =>
        _downloads.DownloadPhotoAsync(galleryId, photoId, context);

    public Task<ControllerServiceResult> UploadPhotoAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context) =>
        _uploads.UploadPhotoAsync(galleryId, file, context);

    public Task<ControllerServiceResult> UploadVideoAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context) =>
        _uploads.UploadVideoAsync(galleryId, file, context);

    public Task<ControllerServiceResult> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request,
        AdminRequestContext context) =>
        _mutations.UpdatePhotoAsync(galleryId, photoId, request, context);

    public Task<ControllerServiceResult> DeletePhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context) =>
        _mutations.DeletePhotoAsync(galleryId, photoId, context);

    public Task<ControllerServiceResult> SetCoverImageAsync(
        int galleryId,
        SetGalleryCoverRequest request,
        AdminRequestContext context) =>
        _mutations.SetCoverImageAsync(galleryId, request, context);

    public Task<ControllerServiceResult> ReorderPhotosAsync(
        int galleryId,
        ReorderGalleryPhotosRequest request,
        AdminRequestContext context) =>
        _mutations.ReorderPhotosAsync(galleryId, request, context);
}