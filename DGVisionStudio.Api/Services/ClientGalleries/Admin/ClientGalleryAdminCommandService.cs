using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryAdminCommandService(
    AppDbContext dbContext,
    IClientGalleryAccessService accessService,
    ClientGalleryNamingService namingService,
    ILogger<ClientGalleryAdminCommandService> logger)
{
    public async Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var galleryType = NormalizeGalleryType(request.GalleryType);
        var status = NormalizeGalleryStatus(galleryType, request.UserGalleryStatus);
        var categoryId = request.IsPublic
            ? request.PortfolioCategoryId ?? await namingService.EnsureClientAlbumsCategoryAsync()
            : await namingService.EnsureClientAlbumsCategoryAsync();

        var maxDisplayOrder = await dbContext.PortfolioAlbums
            .Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var title = request.Title.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn)
            ? null
            : request.TitleEn.Trim();

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = categoryId,
            Slug = await namingService.BuildUniqueSlugAsync(titleEn ?? title),
            Title = title,
            TitleEn = titleEn,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl)
                ? null
                : request.CoverImageUrl.Trim(),
            DisplayOrder = maxDisplayOrder + 1,
            IsPublished = request.IsPublic && request.IsPublished,
            AllowClientAccess = request.IsActive,
            GalleryType = galleryType,
            IsUserUploaded = false,
            UserGalleryStatus = status,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.PortfolioAlbums.Add(album);
        await dbContext.SaveChangesAsync();
        await accessService.SyncUserAccessesAsync(album.Id, request.UserAccesses);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin client gallery created. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, Status: {Status}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, IsActive: {IsActive}",
            album.Id,
            album.Title,
            album.GalleryType,
            album.UserGalleryStatus,
            request.IsPublic,
            request.IsPublished,
            request.IsActive);

        return album.Id;
    }

    public async Task<bool> UpdateGalleryAsync(
        int galleryId,
        AdminUpdateClientGalleryRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var galleryType = NormalizeGalleryType(request.GalleryType);
        var status = NormalizeGalleryStatus(galleryType, request.UserGalleryStatus);
        var title = request.Title.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn)
            ? null
            : request.TitleEn.Trim();

        if (!string.Equals(album.Title, title, StringComparison.Ordinal) ||
            !string.Equals(album.TitleEn, titleEn, StringComparison.Ordinal))
        {
            album.Slug = await namingService.BuildUniqueSlugAsync(titleEn ?? title, galleryId);
        }

        album.Title = title;
        album.TitleEn = titleEn;
        album.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        album.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl)
            ? null
            : request.CoverImageUrl.Trim();
        album.AllowClientAccess = request.IsActive;
        album.IsPublished = request.IsPublic && request.IsPublished;
        album.PortfolioCategoryId = request.IsPublic
            ? request.PortfolioCategoryId ?? album.PortfolioCategoryId
            : await namingService.EnsureClientAlbumsCategoryAsync();
        album.GalleryType = galleryType;
        album.UserGalleryStatus = status;

        if (galleryType == GalleryType.Photoshoot)
        {
            album.IsUserUploaded = false;
            album.OwnerUserId = null;
            album.ExpiresAtUtc = null;
        }

        await dbContext.SaveChangesAsync();
        await accessService.SyncUserAccessesAsync(album.Id, request.UserAccesses);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin client gallery updated. GalleryId: {GalleryId}, Title: {Title}, GalleryType: {GalleryType}, Status: {Status}, IsPublic: {IsPublic}, IsPublished: {IsPublished}, IsActive: {IsActive}",
            album.Id,
            album.Title,
            album.GalleryType,
            album.UserGalleryStatus,
            request.IsPublic,
            request.IsPublished,
            request.IsActive);

        return true;
    }

    public async Task<bool> DeleteGalleryAsync(int galleryId)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var album = await dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);

        if (album == null)
            return false;

        var now = DateTime.UtcNow;
        var imageCount = album.Images.Count;
        var userAccessCount = album.UserAccesses.Count;
        var isUserUploaded = album.IsUserUploaded;
        var ownerUserId = album.OwnerUserId;

        album.IsDeleted = true;
        album.DeletedAtUtc = now;
        album.AllowClientAccess = false;
        album.IsPublished = false;

        foreach (var image in album.Images)
        {
            image.IsDeleted = true;
            image.DeletedAtUtc = now;
            image.IsPublished = false;
            image.IsCover = false;
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogWarning(
            "Client gallery soft deleted. GalleryId: {GalleryId}, ImageCount: {ImageCount}, UserAccessCount: {UserAccessCount}, IsUserUploaded: {IsUserUploaded}, OwnerUserId: {OwnerUserId}",
            galleryId,
            imageCount,
            userAccessCount,
            isUserUploaded,
            ownerUserId);

        return true;
    }

    private static GalleryType NormalizeGalleryType(GalleryType galleryType) =>
        galleryType switch
        {
            GalleryType.Photoshoot => GalleryType.Photoshoot,
            GalleryType.ClientPrintUpload => GalleryType.ClientPrintUpload,
            _ => GalleryType.Photoshoot
        };

    private static UserClientGalleryStatus NormalizeGalleryStatus(
        GalleryType galleryType,
        UserClientGalleryStatus status)
    {
        if (galleryType == GalleryType.ClientPrintUpload)
        {
            return status switch
            {
                UserClientGalleryStatus.Pending => UserClientGalleryStatus.Pending,
                UserClientGalleryStatus.Processed => UserClientGalleryStatus.Processed,
                UserClientGalleryStatus.Expired => UserClientGalleryStatus.Expired,
                _ => UserClientGalleryStatus.Pending
            };
        }

        return status switch
        {
            UserClientGalleryStatus.PhotoshootUploaded => UserClientGalleryStatus.PhotoshootUploaded,
            UserClientGalleryStatus.PhotoshootInProgress => UserClientGalleryStatus.PhotoshootInProgress,
            UserClientGalleryStatus.PhotoshootReadyForPickup => UserClientGalleryStatus.PhotoshootReadyForPickup,
            UserClientGalleryStatus.PhotoshootCancelled => UserClientGalleryStatus.PhotoshootCancelled,
            _ => UserClientGalleryStatus.PhotoshootUploaded
        };
    }
}