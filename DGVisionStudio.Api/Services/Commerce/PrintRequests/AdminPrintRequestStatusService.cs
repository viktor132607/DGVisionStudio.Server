using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestStatusService(
    AppDbContext context)
{
    private static readonly string[] AllowedStatuses =
    [
        "New",
        "InProgress",
        "Completed",
        "Cancelled"
    ];

    public async Task<ControllerServiceResult> UpdateAsync(
        int id,
        UpdatePrintRequestStatusDto dto)
    {
        if (!AllowedStatuses.Contains(dto.Status))
            return ControllerServiceResult.BadRequest("Invalid status.");

        if (id < 0)
            return await UpdateUploadedAlbumAsync(Math.Abs(id), dto.Status);

        var request =
            await context.PrintRequests.FirstOrDefaultAsync(
                item => item.Id == id);

        if (request == null)
            return ControllerServiceResult.NotFound();

        request.Status = dto.Status;
        request.IsSeenByAdmin = true;
        request.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    private async Task<ControllerServiceResult> UpdateUploadedAlbumAsync(
        int albumId,
        string status)
    {
        var album = await context.PortfolioAlbums.FirstOrDefaultAsync(
            item =>
                item.Id == albumId &&
                item.GalleryType == GalleryType.ClientPrintUpload &&
                item.IsUserUploaded &&
                !item.IsDeleted);

        if (album == null)
            return ControllerServiceResult.NotFound();

        album.UserGalleryStatus = status switch
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
}
