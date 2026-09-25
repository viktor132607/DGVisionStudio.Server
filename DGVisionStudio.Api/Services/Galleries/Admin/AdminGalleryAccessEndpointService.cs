using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryAccessEndpointService :
    IAdminGalleryAccessEndpointService
{
    private readonly AdminGalleryAccessQueryService _queries;
    private readonly AdminGalleryAccessMutationService _mutations;

    [ActivatorUtilitiesConstructor]
    public AdminGalleryAccessEndpointService(
        AdminGalleryAccessQueryService queries,
        AdminGalleryAccessMutationService mutations)
    {
        _queries = queries;
        _mutations = mutations;
    }

    public AdminGalleryAccessEndpointService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminGalleryAccessEndpointService> logger)
        : this(
            new AdminGalleryAccessQueryService(clientGalleryService),
            new AdminGalleryAccessMutationService(
                clientGalleryService,
                new AdminGalleryMutationAuditService(auditLogService),
                logger))
    {
    }

    public Task<ControllerServiceResult> GetGalleryAccessesAsync(
        int galleryId) =>
        _queries.GetAsync(galleryId);

    public Task<ControllerServiceResult> GrantAccessAsync(
        int galleryId,
        GrantGalleryAccessRequest request,
        AdminRequestContext context) =>
        _mutations.GrantAsync(
            galleryId,
            request,
            context);

    public Task<ControllerServiceResult> UpdateAccessAsync(
        int galleryId,
        string userId,
        UpdateGalleryAccessRequest request,
        AdminRequestContext context) =>
        _mutations.UpdateAsync(
            galleryId,
            userId,
            request,
            context);

    public Task<ControllerServiceResult> RemoveAccessAsync(
        int galleryId,
        string userId,
        AdminRequestContext context) =>
        _mutations.RemoveAsync(
            galleryId,
            userId,
            context);
}
