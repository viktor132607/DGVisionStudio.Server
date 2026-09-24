using System.Security.Claims;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class ClientGalleryEndpointService
    : IClientGalleryEndpointService
{
    private readonly ClientGalleryUserEndpointService userEndpoints;
    private readonly ClientGalleryPhotoDownloadEndpointService photoDownloads;
    private readonly ClientGalleryZipDownloadService zipDownloads;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryEndpointService(
        ClientGalleryUserEndpointService userEndpoints,
        ClientGalleryPhotoDownloadEndpointService photoDownloads,
        ClientGalleryZipDownloadService zipDownloads)
    {
        this.userEndpoints = userEndpoints;
        this.photoDownloads = photoDownloads;
        this.zipDownloads = zipDownloads;
    }

    public ClientGalleryEndpointService(
        IClientGalleryService clientGalleryService,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IFileStorageService fileStorageService)
    {
        var userContext =
            new ClientGalleryEndpointUserContextService(userManager);

        userEndpoints = new ClientGalleryUserEndpointService(
            clientGalleryService,
            userContext);

        photoDownloads =
            new ClientGalleryPhotoDownloadEndpointService(
                clientGalleryService,
                userContext);

        zipDownloads = new ClientGalleryZipDownloadService(
            clientGalleryService,
            userContext,
            dbContext,
            fileStorageService);
    }

    public Task<ControllerServiceResult> GetMyGalleriesAsync(
        ClaimsPrincipal principal) =>
        userEndpoints.GetMyGalleriesAsync(principal);

    public Task<ControllerServiceResult> CreateMyGalleryAsync(
        ClaimsPrincipal principal,
        CreateUserClientGalleryRequest request) =>
        userEndpoints.CreateMyGalleryAsync(principal, request);

    public Task<ControllerServiceResult> GetGalleryDetailsAsync(
        ClaimsPrincipal principal,
        int galleryId) =>
        userEndpoints.GetGalleryDetailsAsync(principal, galleryId);

    public Task<ControllerServiceResult> UploadMyGalleryPhotoAsync(
        ClaimsPrincipal principal,
        int galleryId,
        IFormFile file) =>
        userEndpoints.UploadMyGalleryPhotoAsync(
            principal,
            galleryId,
            file);

    public Task<ControllerServiceResult> DeleteMyGalleryAsync(
        ClaimsPrincipal principal,
        int galleryId) =>
        userEndpoints.DeleteMyGalleryAsync(principal, galleryId);

    public Task<ControllerServiceResult> DownloadPhotoAsync(
        ClaimsPrincipal principal,
        int galleryId,
        int photoId) =>
        photoDownloads.DownloadPhotoAsync(
            principal,
            galleryId,
            photoId);

    public Task<ControllerServiceResult> DownloadGalleryZipAsync(
        ClaimsPrincipal principal,
        int galleryId) =>
        zipDownloads.DownloadGalleryZipAsync(
            principal,
            galleryId);
}
