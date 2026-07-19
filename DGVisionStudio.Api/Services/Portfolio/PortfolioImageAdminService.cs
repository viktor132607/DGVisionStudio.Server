using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioImageAdminService(
    AppDbContext context,
    IAuditLogService auditLogService,
    ILogger<PortfolioImageAdminService> logger)
{
    public async Task<ControllerServiceResult> GetImagesAsync(PagedQueryDto query)
    {
        var source = context.PortfolioImages
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
            .Where(x => x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded)
            .OrderBy(x => x.PortfolioAlbumId)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id);

        var total = await source.CountAsync();
        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return ControllerServiceResult.Ok(new PagedResultDto<PortfolioImage>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            Items = items
        });
    }

    public async Task<ControllerServiceResult> CreateImageAsync(
        CreatePortfolioImageRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        if (!await context.PortfolioAlbums.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioAlbumId && !x.IsUserUploaded))
            return ControllerServiceResult.BadRequest(new { message = "Невалиден албум." });

        var imageUrl = (model.ImageUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(imageUrl))
            return ControllerServiceResult.BadRequest(new { message = "ImageUrl е задължителен." });

        var entity = new PortfolioImage
        {
            PortfolioAlbumId = model.PortfolioAlbumId,
            ImageUrl = imageUrl,
            ThumbnailUrl = string.IsNullOrWhiteSpace(model.ThumbnailUrl) ? null : model.ThumbnailUrl.Trim(),
            AltText = string.IsNullOrWhiteSpace(model.AltText) ? null : model.AltText.Trim(),
            Caption = string.IsNullOrWhiteSpace(model.Caption) ? null : model.Caption.Trim(),
            Width = model.Width ?? 0,
            Height = model.Height ?? 0,
            DisplayOrder = model.DisplayOrder,
            IsCover = model.IsCover,
            IsPublished = model.IsPublished,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.PortfolioImages.Add(entity);
        await context.SaveChangesAsync();
        await AuditAsync("CreatePortfolioImage", "PortfolioImage", entity.Id.ToString(), null, entity, requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin created portfolio image. ImageId: {ImageId}, AlbumId: {AlbumId}, ImageUrl: {ImageUrl}, IsCover: {IsCover}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
            entity.Id,
            entity.PortfolioAlbumId,
            entity.ImageUrl,
            entity.IsCover,
            entity.IsPublished,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> UpdateImageAsync(
        int id,
        UpdatePortfolioImageRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioImages
            .Include(x => x.PortfolioAlbum)
            .FirstOrDefaultAsync(x => x.Id == id && x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var oldValue = new
        {
            entity.Id,
            entity.PortfolioAlbumId,
            entity.ImageUrl,
            entity.ThumbnailUrl,
            entity.AltText,
            entity.Caption,
            entity.Width,
            entity.Height,
            entity.DisplayOrder,
            entity.IsCover,
            entity.IsPublished,
            entity.IsDeleted,
            entity.DeletedAtUtc
        };

        if (!await context.PortfolioAlbums.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioAlbumId && !x.IsUserUploaded))
            return ControllerServiceResult.BadRequest(new { message = "Невалиден албум." });

        var imageUrl = (model.ImageUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(imageUrl))
            return ControllerServiceResult.BadRequest(new { message = "ImageUrl е задължителен." });

        entity.PortfolioAlbumId = model.PortfolioAlbumId;
        entity.ImageUrl = imageUrl;
        entity.ThumbnailUrl = string.IsNullOrWhiteSpace(model.ThumbnailUrl) ? null : model.ThumbnailUrl.Trim();
        entity.AltText = string.IsNullOrWhiteSpace(model.AltText) ? null : model.AltText.Trim();
        entity.Caption = string.IsNullOrWhiteSpace(model.Caption) ? null : model.Caption.Trim();
        entity.Width = model.Width ?? 0;
        entity.Height = model.Height ?? 0;
        entity.DisplayOrder = model.DisplayOrder;
        entity.IsCover = model.IsCover;
        entity.IsPublished = model.IsPublished;

        await context.SaveChangesAsync();
        await AuditAsync(
            "UpdatePortfolioImage",
            "PortfolioImage",
            entity.Id.ToString(),
            oldValue,
            new
            {
                entity.Id,
                entity.PortfolioAlbumId,
                entity.ImageUrl,
                entity.ThumbnailUrl,
                entity.AltText,
                entity.Caption,
                entity.Width,
                entity.Height,
                entity.DisplayOrder,
                entity.IsCover,
                entity.IsPublished,
                entity.IsDeleted,
                entity.DeletedAtUtc
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin updated portfolio image. ImageId: {ImageId}, AlbumId: {AlbumId}, ImageUrl: {ImageUrl}, IsCover: {IsCover}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
            entity.Id,
            entity.PortfolioAlbumId,
            entity.ImageUrl,
            entity.IsCover,
            entity.IsPublished,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> DeleteImageAsync(
        int id,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioImages
            .Include(x => x.PortfolioAlbum)
            .FirstOrDefaultAsync(x => x.Id == id && x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var now = DateTime.UtcNow;
        var oldValue = new
        {
            entity.Id,
            entity.PortfolioAlbumId,
            entity.ImageUrl,
            entity.ThumbnailUrl,
            entity.AltText,
            entity.Caption,
            entity.Width,
            entity.Height,
            entity.DisplayOrder,
            entity.IsCover,
            entity.IsPublished,
            entity.IsDeleted,
            entity.DeletedAtUtc
        };

        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.IsPublished = false;
        entity.IsCover = false;

        await context.SaveChangesAsync();
        await AuditAsync(
            "SoftDeletePortfolioImage",
            "PortfolioImage",
            id.ToString(),
            oldValue,
            new { DeletedImageId = id, IsDeleted = true, DeletedAtUtc = now },
            requestContext);
        await transaction.CommitAsync();

        logger.LogWarning(
            "Admin soft deleted portfolio image. ImageId: {ImageId}, AlbumId: {AlbumId}, ImageUrl: {ImageUrl}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            oldValue.PortfolioAlbumId,
            oldValue.ImageUrl,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.NoContent();
    }

    private Task AuditAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext requestContext) =>
        auditLogService.LogAsync(
            requestContext.UserId,
            requestContext.Email,
            action,
            entityType,
            entityId,
            oldValue,
            newValue,
            requestContext.RemoteIpAddress,
            requestContext.UserAgent,
            requestContext.TraceId);
}