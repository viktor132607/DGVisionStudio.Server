using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserCreationService
{
    private readonly ClientGalleryUserAlbumCreationService _albumCreation;
    private readonly ClientGalleryUserPhotoUploadService _photoUpload;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryUserCreationService(
        ClientGalleryUserAlbumCreationService albumCreation,
        ClientGalleryUserPhotoUploadService photoUpload)
    {
        _albumCreation = albumCreation;
        _photoUpload = photoUpload;
    }

    public ClientGalleryUserCreationService(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ClientGalleryMapper mapper,
        ClientGalleryUploadValidator uploadValidator,
        ClientGalleryNamingService namingService,
        ILogger<ClientGalleryUserCreationService> logger)
        : this(
            new ClientGalleryUserAlbumCreationService(
                dbContext,
                namingService,
                logger),
            new ClientGalleryUserPhotoUploadService(
                dbContext,
                fileStorageService,
                mapper,
                uploadValidator,
                logger))
    {
    }

    public Task<int?> CreateUserGalleryAsync(
        string userId,
        CreateUserClientGalleryRequest request) =>
        _albumCreation.CreateAsync(userId, request);

    public Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(
        int galleryId,
        string userId,
        IFormFile file) =>
        _photoUpload.UploadAsync(galleryId, userId, file);
}
