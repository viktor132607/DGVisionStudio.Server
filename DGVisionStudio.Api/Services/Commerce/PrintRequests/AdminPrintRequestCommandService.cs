using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestCommandService(AppDbContext context)
{
    public async Task<ControllerServiceResult> UpdateStatusAsync(
        int id,
        UpdatePrintRequestStatusDto dto)
    {
        var allowedStatuses = new[] { "New", "InProgress", "Completed", "Cancelled" };
        if (!allowedStatuses.Contains(dto.Status))
            return ControllerServiceResult.BadRequest("Invalid status.");

        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums.FirstOrDefaultAsync(x =>
                x.Id == albumId &&
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted);

            if (album == null)
                return ControllerServiceResult.NotFound();

            album.UserGalleryStatus = dto.Status switch
            {
                "InProgress" => UserClientGalleryStatus.PrintInProgress,
                "Completed" => UserClientGalleryStatus.Processed,
                "Cancelled" => UserClientGalleryStatus.Expired,
                _ => UserClientGalleryStatus.Pending
            };
            album.IsSeenByAdmin = true;
            await context.SaveChangesAsync();
            return ControllerServiceResult.NoContent();
        }

        var request = await context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
            return ControllerServiceResult.NotFound();

        request.Status = dto.Status;
        request.IsSeenByAdmin = true;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> MarkSeenAsync(int id)
    {
        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums.FirstOrDefaultAsync(x =>
                x.Id == albumId &&
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted);

            if (album == null)
                return ControllerServiceResult.NotFound();

            album.IsSeenByAdmin = true;
            await context.SaveChangesAsync();
            return ControllerServiceResult.NoContent();
        }

        var request = await context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);
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
            .Where(x => !x.IsSeenByAdmin)
            .ToListAsync();

        foreach (var request in requests)
        {
            request.IsSeenByAdmin = true;
            request.UpdatedAtUtc = now;
        }

        var userUploadedAlbums = await context.PortfolioAlbums
            .Where(x =>
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsSeenByAdmin &&
                !x.IsDeleted)
            .ToListAsync();

        foreach (var album in userUploadedAlbums)
            album.IsSeenByAdmin = true;

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x =>
                    x.Id == albumId &&
                    x.GalleryType == GalleryType.ClientPrintUpload &&
                    x.IsUserUploaded &&
                    !x.IsDeleted);

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

        var request = await context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
            return ControllerServiceResult.NotFound();

        context.PrintRequests.Remove(request);
        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }
}