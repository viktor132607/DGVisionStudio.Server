using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoMutationService
{
    private readonly ClientGalleryPhotoUpdateService updates;
    private readonly ClientGalleryPhotoDeleteService deletes;
    private readonly ClientGalleryPhotoCoverService covers;
    private readonly ClientGalleryPhotoReorderService reorder;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryPhotoMutationService(
        ClientGalleryPhotoUpdateService updates,
        ClientGalleryPhotoDeleteService deletes,
        ClientGalleryPhotoCoverService covers,
        ClientGalleryPhotoReorderService reorder)
    {
        this.updates = updates;
        this.deletes = deletes;
        this.covers = covers;
        this.reorder = reorder;
    }

    public ClientGalleryPhotoMutationService(
        AppDbContext dbContext,
        ClientGalleryMapper mapper,
        ILogger<ClientGalleryPhotoMutationService> logger)
    {
        _ = logger;

        covers = new ClientGalleryPhotoCoverService(
            dbContext,
            NullLogger<ClientGalleryPhotoCoverService>.Instance);

        updates = new ClientGalleryPhotoUpdateService(
            dbContext,
            mapper,
            covers,
            NullLogger<ClientGalleryPhotoUpdateService>.Instance);

        deletes = new ClientGalleryPhotoDeleteService(
            dbContext,
            covers,
            NullLogger<ClientGalleryPhotoDeleteService>.Instance);

        reorder = new ClientGalleryPhotoReorderService(
            dbContext,
            NullLogger<ClientGalleryPhotoReorderService>.Instance);
    }

    public Task<ClientPhotoDto?> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request) =>
        updates.UpdatePhotoAsync(
            galleryId,
            photoId,
            request);

    public Task<bool> DeletePhotoAsync(
        int galleryId,
        int photoId) =>
        deletes.DeletePhotoAsync(
            galleryId,
            photoId);

    public Task<bool> SetCoverImageAsync(
        int galleryId,
        string coverImageUrl) =>
        covers.SetCoverImageAsync(
            galleryId,
            coverImageUrl);

    public Task<bool> ReorderPhotosAsync(
        int galleryId,
        List<int> orderedPhotoIds) =>
        reorder.ReorderPhotosAsync(
            galleryId,
            orderedPhotoIds);
}
