using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class AdminClientGalleryManagementService : IAdminClientGalleryManagementService
{
    private readonly AdminClientGalleryQueryService _queries;
    private readonly AdminClientGalleryCommandService _commands;
    private readonly AdminClientGalleryExportService _exports;

    [ActivatorUtilitiesConstructor]
    public AdminClientGalleryManagementService(
        AdminClientGalleryQueryService queries,
        AdminClientGalleryCommandService commands,
        AdminClientGalleryExportService exports)
    {
        _queries = queries;
        _commands = commands;
        _exports = exports;
    }

    public AdminClientGalleryManagementService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<AdminClientGalleryManagementService> logger)
        : this(
            new AdminClientGalleryQueryService(clientGalleryService, userManager),
            new AdminClientGalleryCommandService(
                clientGalleryService,
                auditLogService,
                NullLogger<AdminClientGalleryCommandService>.Instance),
            new AdminClientGalleryExportService(
                dbContext,
                fileStorageService,
                NullLogger<AdminClientGalleryExportService>.Instance))
    {
    }

    public Task<ControllerServiceResult> GetAllGalleriesAsync() =>
        _queries.GetAllGalleriesAsync();

    public Task<ControllerServiceResult> DownloadAllAlbumsAsync(AdminRequestContext context) =>
        _exports.DownloadAllAlbumsAsync(context);

    public Task<ControllerServiceResult> GetAvailableUsersAsync() =>
        _queries.GetAvailableUsersAsync();

    public Task<ControllerServiceResult> GetGalleryByIdAsync(int galleryId) =>
        _queries.GetGalleryByIdAsync(galleryId);

    public Task<ControllerServiceResult> CreateGalleryAsync(
        AdminCreateClientGalleryRequest? request,
        AdminRequestContext context) =>
        _commands.CreateGalleryAsync(request, context);

    public Task<ControllerServiceResult> UpdateGalleryAsync(
        int galleryId,
        AdminUpdateClientGalleryRequest? request,
        AdminRequestContext context) =>
        _commands.UpdateGalleryAsync(galleryId, request, context);

    public Task<ControllerServiceResult> DeleteGalleryAsync(
        int galleryId,
        AdminRequestContext context) =>
        _commands.DeleteGalleryAsync(galleryId, context);
}