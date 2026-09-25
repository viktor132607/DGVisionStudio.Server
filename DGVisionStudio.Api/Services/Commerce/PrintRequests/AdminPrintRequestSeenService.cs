using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestSeenService(
    AppDbContext context)
{
    public async Task<ControllerServiceResult> MarkSeenAsync(int id)
    {
        if (id < 0)
            return await MarkUploadedAlbumSeenAsync(Math.Abs(id));

        var request =
            await context.PrintRequests.FirstOrDefaultAsync(
                item => item.Id == id);

        if (request == null)
            return ControllerServiceResult.NotFound();

        request.IsSeenByAdmin = true;
        request.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> MarkAllSeenAsync()
    {
        var now = DateTime.UtcNow;
        var requests = await context.PrintRequests
            .Where(item => !item.IsSeenByAdmin)
            .ToListAsync();

        foreach (var request in requests)
        {
            request.IsSeenByAdmin = true;
            request.UpdatedAtUtc = now;
        }

        var userUploadedAlbums = await context.PortfolioAlbums
            .Where(item =>
                item.GalleryType == GalleryType.ClientPrintUpload &&
                item.IsUserUploaded &&
                !item.IsSeenByAdmin &&
                !item.IsDeleted)
            .ToListAsync();

        foreach (var album in userUploadedAlbums)
            album.IsSeenByAdmin = true;

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    private async Task<ControllerServiceResult> MarkUploadedAlbumSeenAsync(
        int albumId)
    {
        var album = await context.PortfolioAlbums.FirstOrDefaultAsync(
            item =>
                item.Id == albumId &&
                item.GalleryType == GalleryType.ClientPrintUpload &&
                item.IsUserUploaded &&
                !item.IsDeleted);

        if (album == null)
            return ControllerServiceResult.NotFound();

        album.IsSeenByAdmin = true;

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }
}
