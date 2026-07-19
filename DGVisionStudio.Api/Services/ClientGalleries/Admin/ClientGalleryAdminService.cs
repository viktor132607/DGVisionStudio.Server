using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminService : IClientGalleryAdminService
{
    private readonly ClientGalleryAdminQueryService _queries;
    private readonly ClientGalleryAdminCommandService _commands;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryAdminService(
        ClientGalleryAdminQueryService queries,
        ClientGalleryAdminCommandService commands)
    {
        _queries = queries;
        _commands = commands;
    }

    public ClientGalleryAdminService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IClientGalleryAccessService accessService,
        ClientGalleryMapper mapper,
        ClientGalleryNamingService namingService,
        ILogger<ClientGalleryAdminService> logger)
        : this(
            new ClientGalleryAdminQueryService(dbContext, userManager, mapper),
            new ClientGalleryAdminCommandService(
                dbContext,
                accessService,
                namingService,
                NullLogger<ClientGalleryAdminCommandService>.Instance))
    {
    }

    public Task<List<MyClientGalleryDto>> GetAllGalleriesAsync() =>
        _queries.GetAllGalleriesAsync();

    public Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId) =>
        _queries.GetGalleryByIdAsync(galleryId);

    public Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request) =>
        _commands.CreateGalleryAsync(request);

    public Task<bool> UpdateGalleryAsync(
        int galleryId,
        AdminUpdateClientGalleryRequest request) =>
        _commands.UpdateGalleryAsync(galleryId, request);

    public Task<bool> DeleteGalleryAsync(int galleryId) =>
        _commands.DeleteGalleryAsync(galleryId);
}