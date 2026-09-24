using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAccessMutationService(
    AppDbContext dbContext,
    ILogger<ClientGalleryAccessMutationService> logger)
{
    public async Task<bool> UpdateAccessAsync(
        int galleryId,
        string userId,
        UpdateGalleryAccessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var access = await FindActiveAccessAsync(galleryId, userId);
        if (access == null)
            return false;

        access.PreviewEnabled = request.PreviewEnabled;
        access.DownloadEnabled = request.DownloadEnabled;
        access.DownloadExpiresAtUtc = request.DownloadExpiresAtUtc;

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "Gallery access updated. GalleryId: {GalleryId}, UserId: {UserId}, PreviewEnabled: {PreviewEnabled}, DownloadEnabled: {DownloadEnabled}, DownloadExpiresAtUtc: {DownloadExpiresAtUtc}",
            galleryId,
            userId,
            request.PreviewEnabled,
            request.DownloadEnabled,
            request.DownloadExpiresAtUtc);

        return true;
    }

    public async Task<bool> RemoveAccessAsync(int galleryId, string userId)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var access = await FindActiveAccessAsync(galleryId, userId);
        if (access == null)
            return false;

        dbContext.UserAlbumAccesses.Remove(access);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogWarning(
            "Gallery access removed. GalleryId: {GalleryId}, UserId: {UserId}",
            galleryId,
            userId);

        return true;
    }

    private Task<Domain.Entities.UserAlbumAccess?> FindActiveAccessAsync(
        int galleryId,
        string userId) =>
        dbContext.UserAlbumAccesses
            .Include(x => x.PortfolioAlbum)
            .FirstOrDefaultAsync(x =>
                x.PortfolioAlbumId == galleryId &&
                x.UserId == userId &&
                !x.PortfolioAlbum.IsDeleted);
}
