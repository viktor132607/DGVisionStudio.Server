using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services;

public sealed class ClientGalleryUserEndpointService(
    IClientGalleryService clientGalleryService,
    ClientGalleryEndpointUserContextService userContext)
{
    public async Task<ControllerServiceResult> GetMyGalleriesAsync(
        ClaimsPrincipal principal)
    {
        var user = await userContext.ResolveAsync(principal);
        return user is null
            ? ClientGalleryEndpointUserContextService.Unauthenticated()
            : ControllerServiceResult.Ok(
                await clientGalleryService.GetMyGalleriesAsync(user.Id));
    }

    public async Task<ControllerServiceResult> CreateMyGalleryAsync(
        ClaimsPrincipal principal,
        CreateUserClientGalleryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userContext.ResolveAsync(principal);
        if (user is null)
            return ClientGalleryEndpointUserContextService.Unauthenticated();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Title is required." });
        }

        var galleryId = await clientGalleryService
            .CreateUserGalleryAsync(user.Id, request);

        return galleryId is null
            ? ControllerServiceResult.BadRequest(new
            {
                message = "You can have up to 10 active galleries. Each gallery expires after 7 days."
            })
            : ControllerServiceResult.Ok(new
            {
                message = "Gallery created successfully.",
                id = galleryId.Value
            });
    }

    public async Task<ControllerServiceResult> GetGalleryDetailsAsync(
        ClaimsPrincipal principal,
        int galleryId)
    {
        var user = await userContext.ResolveAsync(principal);
        if (user is null)
            return ClientGalleryEndpointUserContextService.Unauthenticated();

        var gallery = await clientGalleryService
            .GetGalleryDetailsAsync(galleryId, user.Id);

        return gallery is null
            ? ControllerServiceResult.NotFound(
                new { message = "Gallery not found or access denied." })
            : ControllerServiceResult.Ok(gallery);
    }

    public async Task<ControllerServiceResult> UploadMyGalleryPhotoAsync(
        ClaimsPrincipal principal,
        int galleryId,
        IFormFile file)
    {
        var user = await userContext.ResolveAsync(principal);
        if (user is null)
            return ClientGalleryEndpointUserContextService.Unauthenticated();

        if (file is null || file.Length == 0)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "File is required." });
        }

        var photo = await clientGalleryService
            .UploadUserGalleryPhotoAsync(
                galleryId,
                user.Id,
                file);

        return photo is null
            ? ControllerServiceResult.BadRequest(
                new { message = "Gallery not found, expired, or access denied." })
            : ControllerServiceResult.Ok(photo);
    }

    public async Task<ControllerServiceResult> DeleteMyGalleryAsync(
        ClaimsPrincipal principal,
        int galleryId)
    {
        var user = await userContext.ResolveAsync(principal);
        if (user is null)
            return ClientGalleryEndpointUserContextService.Unauthenticated();

        var deleted = await clientGalleryService
            .DeleteUserGalleryAsync(galleryId, user.Id);

        return deleted
            ? ControllerServiceResult.Ok(
                new { message = "Gallery deleted successfully." })
            : ControllerServiceResult.NotFound(
                new { message = "Gallery not found or access denied." });
    }
}
