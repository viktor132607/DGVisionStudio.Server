using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryAccessService : IClientGalleryAccessService
{
    private readonly ClientGalleryAccessQueryService queries;
    private readonly ClientGalleryAccessGrantService grants;
    private readonly ClientGalleryAccessMutationService mutations;
    private readonly ClientGalleryAccessSyncService sync;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryAccessService(
        ClientGalleryAccessQueryService queries,
        ClientGalleryAccessGrantService grants,
        ClientGalleryAccessMutationService mutations,
        ClientGalleryAccessSyncService sync)
    {
        this.queries = queries;
        this.grants = grants;
        this.mutations = mutations;
        this.sync = sync;
    }

    public ClientGalleryAccessService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<ClientGalleryAccessService> logger)
    {
        _ = logger;

        queries = new ClientGalleryAccessQueryService(dbContext);
        grants = new ClientGalleryAccessGrantService(
            dbContext,
            userManager,
            NullLogger<ClientGalleryAccessGrantService>.Instance);
        mutations = new ClientGalleryAccessMutationService(
            dbContext,
            NullLogger<ClientGalleryAccessMutationService>.Instance);
        sync = new ClientGalleryAccessSyncService(dbContext, userManager);
    }

    public Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId) =>
        queries.GetGalleryAccessesAsync(galleryId);

    public Task<bool> GrantAccessAsync(
        int galleryId,
        GrantGalleryAccessRequest request) =>
        grants.GrantAccessAsync(galleryId, request);

    public Task<bool> UpdateAccessAsync(
        int galleryId,
        string userId,
        UpdateGalleryAccessRequest request) =>
        mutations.UpdateAccessAsync(galleryId, userId, request);

    public Task<bool> RemoveAccessAsync(int galleryId, string userId) =>
        mutations.RemoveAccessAsync(galleryId, userId);

    public Task SyncUserAccessesAsync(
        int galleryId,
        List<GalleryUserAccessDto>? requestedAccesses) =>
        sync.SyncUserAccessesAsync(galleryId, requestedAccesses);
}
