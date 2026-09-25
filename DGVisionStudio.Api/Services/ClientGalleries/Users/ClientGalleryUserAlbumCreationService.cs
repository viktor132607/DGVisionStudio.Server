using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryUserAlbumCreationService(
    AppDbContext dbContext,
    ClientGalleryNamingService namingService,
    ILogger<ClientGalleryUserCreationService> logger)
{
    private const int MaxUserUploadedGalleries = 10;
    private const int UserUploadedGalleryLifetimeDays = 7;

    public async Task<int?> CreateAsync(
        string userId,
        CreateUserClientGalleryRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        var activeUserGalleryCount = await dbContext.PortfolioAlbums
            .CountAsync(x =>
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                x.OwnerUserId == userId &&
                x.AllowClientAccess &&
                !x.IsDeleted &&
                x.ExpiresAtUtc != null &&
                x.ExpiresAtUtc > now);

        if (activeUserGalleryCount >= MaxUserUploadedGalleries)
        {
            logger.LogWarning(
                "User client gallery creation rejected because limit was reached. UserId: {UserId}, ActiveGalleryCount: {ActiveGalleryCount}, Limit: {Limit}",
                userId,
                activeUserGalleryCount,
                MaxUserUploadedGalleries);
            return null;
        }

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var categoryId = await namingService.EnsureClientAlbumsCategoryAsync();
        var maxDisplayOrder = await dbContext.PortfolioAlbums
            .Where(x => x.PortfolioCategoryId == categoryId && !x.IsDeleted)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = categoryId,
            Slug = await namingService.BuildUniqueSlugAsync(title),
            Title = title,
            TitleEn = null,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            CoverImageUrl = null,
            DisplayOrder = maxDisplayOrder + 1,
            IsPublished = false,
            AllowClientAccess = true,
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            OwnerUserId = userId,
            ExpiresAtUtc = now.AddDays(UserUploadedGalleryLifetimeDays),
            UserGalleryStatus = UserClientGalleryStatus.Pending,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = now
        };

        dbContext.PortfolioAlbums.Add(album);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation(
            "User client gallery created. GalleryId: {GalleryId}, OwnerUserId: {OwnerUserId}, Title: {Title}, GalleryType: {GalleryType}, ExpiresAtUtc: {ExpiresAtUtc}, Status: {Status}",
            album.Id,
            album.OwnerUserId,
            album.Title,
            album.GalleryType,
            album.ExpiresAtUtc,
            album.UserGalleryStatus);

        return album.Id;
    }
}
