using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryAccessQueryService(
    IClientGalleryService clientGalleryService)
{
    public async Task<ControllerServiceResult> GetAsync(int galleryId)
    {
        if (galleryId <= 0)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Invalid gallery id." });
        }

        var gallery =
            await clientGalleryService.GetGalleryByIdAsync(galleryId);

        if (gallery is null)
        {
            return ControllerServiceResult.NotFound(
                new { message = "Gallery not found." });
        }

        return ControllerServiceResult.Ok(
            await clientGalleryService.GetGalleryAccessesAsync(galleryId));
    }
}
