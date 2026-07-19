using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoService : IClientGalleryPhotoService
{
    private readonly ClientGalleryPhotoDownloadService _downloads;
    private readonly ClientGalleryPhotoUploadService _uploads;
    private readonly ClientGalleryPhotoMutationService _mutations;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryPhotoService(
        ClientGalleryPhotoDownloadService downloads,
        ClientGalleryPhotoUploadService uploads,
        ClientGalleryPhotoMutationService mutations)
    {
        _downloads = downloads;
        _uploads = uploads;
        _mutations = mutations;
    }

    public ClientGalleryPhotoService(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ClientGalleryMapper mapper,
        ClientGalleryUploadValidator uploadValidator,
        ClientGalleryNamingService namingService,
        ILogger<ClientGalleryPhotoService> logger)
        : this(
            new ClientGalleryPhotoDownloadService(
                dbContext,
                fileStorageService,
                mapper,
                namingService,
                NullLogger<ClientGalleryPhotoDownloadService>.Instance),
            new ClientGalleryPhotoUploadService(
                dbContext,
                fileStorageService,
                mapper,
                uploadValidator,
                NullLogger<ClientGalleryPhotoUploadService>.Instance),
            new ClientGalleryPhotoMutationService(
                dbContext,
                mapper,
                NullLogger<ClientGalleryPhotoMutationService>.Instance))
    {
    }

    public Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(
        int galleryId,
        int photoId,
        string userId,
        bool isAdmin) =>
        _downloads.OpenPhotoDownloadAsync(galleryId, photoId, userId, isAdmin);

    public Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file) =>
        _uploads.UploadPhotoAsync(galleryId, file);

    public Task<ClientPhotoDto?> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request) =>
        _mutations.UpdatePhotoAsync(galleryId, photoId, request);

    public Task<bool> DeletePhotoAsync(int galleryId, int photoId) =>
        _mutations.DeletePhotoAsync(galleryId, photoId);

    public Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl) =>
        _mutations.SetCoverImageAsync(galleryId, coverImageUrl);

    public Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds) =>
        _mutations.ReorderPhotosAsync(galleryId, orderedPhotoIds);
}