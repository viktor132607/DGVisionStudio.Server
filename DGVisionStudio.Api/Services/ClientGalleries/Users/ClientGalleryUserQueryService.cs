using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserQueryService
{
    private readonly ClientGalleryUserListQueryService _list;
    private readonly ClientGalleryUserDetailsQueryService _details;
    private readonly ClientGalleryUserAccessQueryService _access;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryUserQueryService(
        ClientGalleryUserListQueryService list,
        ClientGalleryUserDetailsQueryService details,
        ClientGalleryUserAccessQueryService access)
    {
        _list = list;
        _details = details;
        _access = access;
    }

    public ClientGalleryUserQueryService(
        AppDbContext dbContext,
        ClientGalleryMapper mapper)
        : this(
            new ClientGalleryUserListQueryService(
                dbContext,
                mapper),
            new ClientGalleryUserDetailsQueryService(
                dbContext,
                mapper),
            new ClientGalleryUserAccessQueryService(
                dbContext,
                mapper))
    {
    }

    public Task<List<MyClientGalleryDto>>
        GetMyGalleriesAsync(string userId) =>
        _list.GetMyGalleriesAsync(userId);

    public Task<ClientGalleryDetailsDto?>
        GetGalleryDetailsAsync(
            int galleryId,
            string userId) =>
        _details.GetGalleryDetailsAsync(
            galleryId,
            userId);

    public Task<bool> UserCanAccessGalleryAsync(
        int galleryId,
        string userId,
        bool requireDownload) =>
        _access.UserCanAccessGalleryAsync(
            galleryId,
            userId,
            requireDownload);
}
