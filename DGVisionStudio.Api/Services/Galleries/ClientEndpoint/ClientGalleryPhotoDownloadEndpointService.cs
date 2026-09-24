using System.Security.Claims;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class ClientGalleryPhotoDownloadEndpointService(
    IClientGalleryService clientGalleryService,
    ClientGalleryEndpointUserContextService userContext)
{
    public async Task<ControllerServiceResult> DownloadPhotoAsync(
        ClaimsPrincipal principal,
        int galleryId,
        int photoId)
    {
        var user = await userContext.ResolveAsync(principal);
        if (user is null)
            return ClientGalleryEndpointUserContextService.Unauthenticated();

        var result = await clientGalleryService.OpenPhotoDownloadAsync(
            galleryId,
            photoId,
            user.Id,
            principal.IsInRole("Admin"));

        return result is null
            ? ControllerServiceResult.NotFound(
                new { message = "Photo not found or access denied." })
            : ControllerServiceResult.Ok(new FileDownloadResult(
                result.Value.Stream,
                result.Value.ContentType,
                result.Value.FileName));
    }
}
