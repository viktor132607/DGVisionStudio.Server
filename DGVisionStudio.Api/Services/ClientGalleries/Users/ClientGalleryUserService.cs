using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserService : IClientGalleryUserService
{
    private readonly ClientGalleryUserQueryService _queries;
    private readonly ClientGalleryUserCreationService _creation;
    private readonly ClientGalleryUserLifecycleService _lifecycle;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryUserService(
        ClientGalleryUserQueryService queries,
        ClientGalleryUserCreationService creation,
        ClientGalleryUserLifecycleService lifecycle)
    {
        _queries = queries;
        _creation = creation;
        _lifecycle = lifecycle;
    }

    public ClientGalleryUserService(
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ClientGalleryMapper mapper,
        ClientGalleryUploadValidator uploadValidator,
        ClientGalleryNamingService namingService,
        ILogger<ClientGalleryUserService> logger)
        : this(
            new ClientGalleryUserQueryService(dbContext, mapper),
            new ClientGalleryUserCreationService(
                dbContext,
                fileStorageService,
                mapper,
                uploadValidator,
                namingService,
                NullLogger<ClientGalleryUserCreationService>.Instance),
            new ClientGalleryUserLifecycleService(
                dbContext,
                fileStorageService,
                NullLogger<ClientGalleryUserLifecycleService>.Instance))
    {
    }

    public Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId) =>
        _queries.GetMyGalleriesAsync(userId);

    public Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(
        int galleryId,
        string userId) =>
        _queries.GetGalleryDetailsAsync(galleryId, userId);

    public Task<int?> CreateUserGalleryAsync(
        string userId,
        CreateUserClientGalleryRequest request) =>
        _creation.CreateUserGalleryAsync(userId, request);

    public Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(
        int galleryId,
        string userId,
        IFormFile file) =>
        _creation.UploadUserGalleryPhotoAsync(galleryId, userId, file);

    public Task<bool> DeleteUserGalleryAsync(int galleryId, string userId) =>
        _lifecycle.DeleteUserGalleryAsync(galleryId, userId);

    public Task<bool> UserCanAccessGalleryAsync(
        int galleryId,
        string userId,
        bool requireDownload) =>
        _queries.UserCanAccessGalleryAsync(galleryId, userId, requireDownload);
}