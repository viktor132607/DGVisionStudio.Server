using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminCommandService
{
    private readonly ClientGalleryAdminCreateService createService;
    private readonly ClientGalleryAdminUpdateService updateService;
    private readonly ClientGalleryAdminLifecycleService lifecycleService;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryAdminCommandService(
        ClientGalleryAdminCreateService createService,
        ClientGalleryAdminUpdateService updateService,
        ClientGalleryAdminLifecycleService lifecycleService)
    {
        this.createService = createService;
        this.updateService = updateService;
        this.lifecycleService = lifecycleService;
    }

    public ClientGalleryAdminCommandService(
        AppDbContext dbContext,
        IClientGalleryAccessService accessService,
        ClientGalleryNamingService namingService,
        ILogger<ClientGalleryAdminCommandService> logger)
    {
        _ = logger;

        var normalizer = new ClientGalleryAdminInputNormalizer();
        var categoryService = new ClientGalleryAdminCategoryService(
            dbContext,
            namingService);
        var mapper = new ClientGalleryAdminAlbumMapper();

        createService = new ClientGalleryAdminCreateService(
            dbContext,
            accessService,
            namingService,
            normalizer,
            categoryService,
            mapper,
            NullLogger<ClientGalleryAdminCreateService>.Instance);

        updateService = new ClientGalleryAdminUpdateService(
            dbContext,
            accessService,
            namingService,
            normalizer,
            categoryService,
            mapper,
            NullLogger<ClientGalleryAdminUpdateService>.Instance);

        lifecycleService = new ClientGalleryAdminLifecycleService(
            dbContext,
            NullLogger<ClientGalleryAdminLifecycleService>.Instance);
    }

    public Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request) =>
        createService.CreateGalleryAsync(request);

    public Task<bool> UpdateGalleryAsync(
        int galleryId,
        AdminUpdateClientGalleryRequest request) =>
        updateService.UpdateGalleryAsync(galleryId, request);

    public Task<bool> DeleteGalleryAsync(int galleryId) =>
        lifecycleService.DeleteGalleryAsync(galleryId);
}
