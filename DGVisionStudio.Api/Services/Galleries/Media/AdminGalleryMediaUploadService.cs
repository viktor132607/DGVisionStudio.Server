using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaUploadService
{
    private readonly AdminGalleryPhotoUploadService _photoUploads;
    private readonly AdminGalleryVideoUploadService _videoUploads;

    [ActivatorUtilitiesConstructor]
    public AdminGalleryMediaUploadService(
        AdminGalleryPhotoUploadService photoUploads,
        AdminGalleryVideoUploadService videoUploads)
    {
        _photoUploads = photoUploads;
        _videoUploads = videoUploads;
    }

    public AdminGalleryMediaUploadService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminGalleryMediaUploadService> logger,
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        ClientGalleryMapper mapper)
        : this(
            new AdminGalleryPhotoUploadService(
                clientGalleryService,
                auditLogService,
                logger),
            new AdminGalleryVideoUploadService(
                auditLogService,
                logger,
                dbContext,
                new AdminGalleryVideoFileStorageService(environment),
                mapper))
    {
    }

    public Task<ControllerServiceResult> UploadPhotoAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context) =>
        _photoUploads.UploadAsync(galleryId, file, context);

    public Task<ControllerServiceResult> UploadVideoAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context) =>
        _videoUploads.UploadAsync(galleryId, file, context);
}
