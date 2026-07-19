using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumAdminService(
    AppDbContext context,
    IAuditLogService auditLogService,
    ILogger<PortfolioAlbumAdminService> logger)
{
    public async Task<ControllerServiceResult> GetAlbumsAsync(PagedQueryDto query)
    {
        var source = context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Where(x => !x.IsUserUploaded)
            .OrderBy(x => x.PortfolioCategoryId)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id);

        var total = await source.CountAsync();
        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return ControllerServiceResult.Ok(new PagedResultDto<PortfolioAlbum>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            Items = items
        });
    }

    public async Task<ControllerServiceResult> CreateAlbumAsync(
        CreatePortfolioAlbumRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        if (!await context.PortfolioCategories.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioCategoryId))
            return ControllerServiceResult.BadRequest(new { message = "Невалидна категория." });

        var slug = (model.Slug ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(slug))
            return ControllerServiceResult.BadRequest(new { message = "Slug е задължителен." });

        var titleBg = (model.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titleBg))
            return ControllerServiceResult.BadRequest(new { message = "Заглавието на български е задължително." });

        var duplicateSlugExists = await context.PortfolioAlbums
            .AsNoTracking()
            .AnyAsync(x => x.Slug == slug);

        if (duplicateSlugExists)
            return ControllerServiceResult.BadRequest(new { message = "Вече съществува албум със същия slug." });

        var entity = new PortfolioAlbum
        {
            PortfolioCategoryId = model.PortfolioCategoryId,
            Slug = slug,
            Title = titleBg,
            TitleEn = string.IsNullOrWhiteSpace(model.TitleEn) ? null : model.TitleEn.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(model.CoverImageUrl) ? null : model.CoverImageUrl.Trim(),
            DisplayOrder = model.DisplayOrder,
            ColumnNumber = model.ColumnNumber,
            IsPublished = model.IsPublished,
            IsUserUploaded = false,
            IsSeenByAdmin = true,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.PortfolioAlbums.Add(entity);
        await context.SaveChangesAsync();
        await AuditAsync("CreatePortfolioAlbum", "PortfolioAlbum", entity.Id.ToString(), null, entity, requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin created portfolio album. AlbumId: {AlbumId}, Slug: {Slug}, Title: {Title}, CategoryId: {CategoryId}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
            entity.Id,
            entity.Slug,
            entity.Title,
            entity.PortfolioCategoryId,
            entity.IsPublished,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> UpdateAlbumAsync(
        int id,
        UpdatePortfolioAlbumRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioAlbums.FirstOrDefaultAsync(x => x.Id == id && !x.IsUserUploaded);
        if (entity == null)
            return ControllerServiceResult.NotFound();

        var oldValue = new
        {
            entity.Id,
            entity.PortfolioCategoryId,
            entity.Slug,
            entity.Title,
            entity.TitleEn,
            entity.Description,
            entity.CoverImageUrl,
            entity.DisplayOrder,
            entity.ColumnNumber,
            entity.IsPublished,
            entity.IsDeleted,
            entity.DeletedAtUtc
        };

        if (!await context.PortfolioCategories.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioCategoryId))
            return ControllerServiceResult.BadRequest(new { message = "Невалидна категория." });

        var slug = (model.Slug ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(slug))
            return ControllerServiceResult.BadRequest(new { message = "Slug е задължителен." });

        var titleBg = (model.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titleBg))
            return ControllerServiceResult.BadRequest(new { message = "Заглавието на български е задължително." });

        var duplicateSlugExists = await context.PortfolioAlbums
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Slug == slug);

        if (duplicateSlugExists)
            return ControllerServiceResult.BadRequest(new { message = "Вече съществува албум със същия slug." });

        entity.PortfolioCategoryId = model.PortfolioCategoryId;
        entity.Slug = slug;
        entity.Title = titleBg;
        entity.TitleEn = string.IsNullOrWhiteSpace(model.TitleEn) ? null : model.TitleEn.Trim();
        entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        entity.CoverImageUrl = string.IsNullOrWhiteSpace(model.CoverImageUrl) ? null : model.CoverImageUrl.Trim();
        entity.DisplayOrder = model.DisplayOrder;
        entity.ColumnNumber = model.ColumnNumber;
        entity.IsPublished = model.IsPublished;

        await context.SaveChangesAsync();
        await AuditAsync(
            "UpdatePortfolioAlbum",
            "PortfolioAlbum",
            entity.Id.ToString(),
            oldValue,
            new
            {
                entity.Id,
                entity.PortfolioCategoryId,
                entity.Slug,
                entity.Title,
                entity.TitleEn,
                entity.Description,
                entity.CoverImageUrl,
                entity.DisplayOrder,
                entity.ColumnNumber,
                entity.IsPublished,
                entity.IsDeleted,
                entity.DeletedAtUtc
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin updated portfolio album. AlbumId: {AlbumId}, Slug: {Slug}, Title: {Title}, CategoryId: {CategoryId}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
            entity.Id,
            entity.Slug,
            entity.Title,
            entity.PortfolioCategoryId,
            entity.IsPublished,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> DeleteAlbumAsync(
        int id,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsUserUploaded);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var now = DateTime.UtcNow;
        var oldValue = new
        {
            entity.Id,
            entity.PortfolioCategoryId,
            entity.Slug,
            entity.Title,
            entity.TitleEn,
            entity.Description,
            entity.CoverImageUrl,
            entity.DisplayOrder,
            entity.ColumnNumber,
            entity.IsPublished,
            entity.IsDeleted,
            entity.DeletedAtUtc
        };

        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.IsPublished = false;
        entity.AllowClientAccess = false;

        foreach (var image in entity.Images)
        {
            image.IsDeleted = true;
            image.DeletedAtUtc = now;
            image.IsPublished = false;
            image.IsCover = false;
        }

        await context.SaveChangesAsync();
        await AuditAsync(
            "SoftDeletePortfolioAlbum",
            "PortfolioAlbum",
            id.ToString(),
            oldValue,
            new
            {
                DeletedAlbumId = id,
                SoftDeletedImageCount = entity.Images.Count,
                IsDeleted = true,
                DeletedAtUtc = now
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogWarning(
            "Admin soft deleted portfolio album. AlbumId: {AlbumId}, Slug: {Slug}, Title: {Title}, SoftDeletedImageCount: {SoftDeletedImageCount}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            oldValue.Slug,
            oldValue.Title,
            entity.Images.Count,
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