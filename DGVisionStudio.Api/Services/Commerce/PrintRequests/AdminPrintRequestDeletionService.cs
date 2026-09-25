using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestDeletionService(
    AppDbContext context)
{
    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        if (id < 0)
            return await DeleteUploadedAlbumAsync(Math.Abs(id));

        var request =
            await context.PrintRequests.FirstOrDefaultAsync(
                item => item.Id == id);

        if (request == null)
            return ControllerServiceResult.NotFound();

        context.PrintRequests.Remove(request);
        await context.SaveChangesAsync();

        return ControllerServiceResult.NoContent();
    }

    private async Task<ControllerServiceResult> DeleteUploadedAlbumAsync(
        int albumId)
    {
        var album = await context.PortfolioAlbums
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item =>
                item.Id == albumId &&
                item.GalleryType == GalleryType.ClientPrintUpload &&
                item.IsUserUploaded &&
                !item.IsDeleted);

        if (album == null)
            return ControllerServiceResult.NotFound();

        var now = DateTime.UtcNow;
        album.IsDeleted = true;
        album.DeletedAtUtc = now;
        album.IsPublished = false;
        album.AllowClientAccess = false;
        album.IsSeenByAdmin = true;
        album.UserGalleryStatus = UserClientGalleryStatus.Expired;

        foreach (var image in album.Images)
        {
            image.IsDeleted = true;
            image.DeletedAtUtc = now;
            image.IsPublished = false;
            image.IsCover = false;
        }

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }
}
