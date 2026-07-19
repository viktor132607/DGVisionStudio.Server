using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryAdminService(
    AppDbContext context,
    IAuditLogService auditLogService,
    ILogger<PortfolioCategoryAdminService> logger)
{
    public async Task<ControllerServiceResult> GetCategoriesAsync()
    {
        var items = await context.PortfolioCategories
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return ControllerServiceResult.Ok(items);
    }

    public async Task<ControllerServiceResult> GetCategoryByIdAsync(int id)
    {
        var entity = await context.PortfolioCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity == null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> CreateCategoryAsync(
        CreatePortfolioCategoryRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var key = (model.Key ?? string.Empty).Trim().ToLowerInvariant();
        var name = (model.Name ?? string.Empty).Trim();
        var nameEn = (model.NameEn ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return ControllerServiceResult.BadRequest(new { message = "Името на български е задължително." });

        if (string.IsNullOrWhiteSpace(nameEn))
            return ControllerServiceResult.BadRequest(new { message = "Името на английски е задължително." });

        if (string.IsNullOrWhiteSpace(key))
            return ControllerServiceResult.BadRequest(new { message = "Ключът е задължителен." });

        var duplicateKeyExists = await context.PortfolioCategories
            .AsNoTracking()
            .AnyAsync(x => x.Key.ToLower() == key);

        if (duplicateKeyExists)
            return ControllerServiceResult.BadRequest(new { message = "Вече съществува категория със същия ключ." });

        var entity = new PortfolioCategory
        {
            Key = key,
            Name = name,
            NameEn = nameEn,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            DisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder,
            IsActive = model.IsActive,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        context.PortfolioCategories.Add(entity);
        await context.SaveChangesAsync();
        await AuditAsync("CreatePortfolioCategory", "PortfolioCategory", entity.Id.ToString(), null, entity, requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin created portfolio category. CategoryId: {CategoryId}, Key: {Key}, Name: {Name}, IsActive: {IsActive}, Admin: {Admin}, TraceId: {TraceId}",
            entity.Id,
            entity.Key,
            entity.Name,
            entity.IsActive,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> UpdateCategoryAsync(
        int id,
        UpdatePortfolioCategoryRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioCategories.FindAsync(id);
        if (entity == null)
            return ControllerServiceResult.NotFound();

        var oldValue = new
        {
            entity.Id,
            entity.Key,
            entity.Name,
            entity.NameEn,
            entity.Description,
            entity.DisplayOrder,
            entity.IsActive,
            entity.IsDeleted,
            entity.DeletedAtUtc
        };

        var key = (model.Key ?? string.Empty).Trim().ToLowerInvariant();
        var name = (model.Name ?? string.Empty).Trim();
        var nameEn = (model.NameEn ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return ControllerServiceResult.BadRequest(new { message = "Името на български е задължително." });

        if (string.IsNullOrWhiteSpace(nameEn))
            return ControllerServiceResult.BadRequest(new { message = "Името на английски е задължително." });

        if (string.IsNullOrWhiteSpace(key))
            return ControllerServiceResult.BadRequest(new { message = "Ключът е задължителен." });

        var duplicateKeyExists = await context.PortfolioCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Key.ToLower() == key);

        if (duplicateKeyExists)
            return ControllerServiceResult.BadRequest(new { message = "Вече съществува категория със същия ключ." });

        entity.Key = key;
        entity.Name = name;
        entity.NameEn = nameEn;
        entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        entity.DisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder;
        entity.IsActive = model.IsActive;

        await context.SaveChangesAsync();
        await AuditAsync(
            "UpdatePortfolioCategory",
            "PortfolioCategory",
            entity.Id.ToString(),
            oldValue,
            new
            {
                entity.Id,
                entity.Key,
                entity.Name,
                entity.NameEn,
                entity.Description,
                entity.DisplayOrder,
                entity.IsActive,
                entity.IsDeleted,
                entity.DeletedAtUtc
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin updated portfolio category. CategoryId: {CategoryId}, Key: {Key}, Name: {Name}, IsActive: {IsActive}, Admin: {Admin}, TraceId: {TraceId}",
            entity.Id,
            entity.Key,
            entity.Name,
            entity.IsActive,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> MoveCategoryAsync(
        int id,
        MovePortfolioCategoryRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioCategories.FindAsync(id);
        if (entity == null)
            return ControllerServiceResult.NotFound();

        var oldValue = new { entity.Id, entity.DisplayOrder };
        var categories = await context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var source = categories.FirstOrDefault(x => x.Id == id);
        if (source == null)
            return ControllerServiceResult.NotFound();

        categories.Remove(source);
        var requestedDisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder;
        var targetIndex = Math.Min(requestedDisplayOrder - 1, categories.Count);
        categories.Insert(targetIndex, source);

        for (var i = 0; i < categories.Count; i++)
            categories[i].DisplayOrder = i + 1;

        await context.SaveChangesAsync();
        await AuditAsync(
            "MovePortfolioCategory",
            "PortfolioCategory",
            id.ToString(),
            oldValue,
            new
            {
                CategoryId = id,
                RequestedDisplayOrder = model.DisplayOrder,
                NewDisplayOrder = source.DisplayOrder
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin moved portfolio category. CategoryId: {CategoryId}, RequestedDisplayOrder: {RequestedDisplayOrder}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            model.DisplayOrder,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.Ok(categories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id));
    }

    public async Task<ControllerServiceResult> GetCategoryAlbumsAsync(int id)
    {
        var category = await context.PortfolioCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (category == null)
            return ControllerServiceResult.NotFound();

        var albums = await context.PortfolioAlbums
            .AsNoTracking()
            .Where(x => !x.IsUserUploaded)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Slug,
                x.CoverImageUrl,
                x.DisplayOrder,
                x.IsPublished,
                x.PortfolioCategoryId,
                IsSelected = x.PortfolioCategoryId == id
            })
            .ToListAsync();

        return ControllerServiceResult.Ok(new { category, albums });
    }

    public async Task<ControllerServiceResult> UpdateCategoryAlbumsAsync(
        int id,
        UpdateCategoryAlbumsRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var categoryExists = await context.PortfolioCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);

        if (!categoryExists)
            return ControllerServiceResult.NotFound();

        var selectedAlbumIds = (model.AlbumIds ?? new List<int>()).Distinct().ToHashSet();
        var albums = await context.PortfolioAlbums
            .Where(x => !x.IsUserUploaded)
            .ToListAsync();

        var oldValue = albums
            .Where(x => x.PortfolioCategoryId == id)
            .Select(x => new { x.Id, x.Title, x.PortfolioCategoryId })
            .ToList();

        foreach (var album in albums)
        {
            if (selectedAlbumIds.Contains(album.Id))
                album.PortfolioCategoryId = id;
        }

        await context.SaveChangesAsync();
        await AuditAsync(
            "UpdatePortfolioCategoryAlbums",
            "PortfolioCategory",
            id.ToString(),
            oldValue,
            new { CategoryId = id, SelectedAlbumIds = selectedAlbumIds },
            requestContext);
        await transaction.CommitAsync();

        logger.LogInformation(
            "Admin updated category albums. CategoryId: {CategoryId}, SelectedAlbumCount: {SelectedAlbumCount}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            selectedAlbumIds.Count,
            requestContext.DisplayName,
            requestContext.TraceId);

        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> DeleteCategoryAsync(
        int id,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioCategories
            .Include(x => x.Albums)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var now = DateTime.UtcNow;
        var oldValue = new
        {
            entity.Id,
            entity.Key,
            entity.Name,
            entity.NameEn,
            entity.Description,
            entity.DisplayOrder,
            entity.IsActive,
            entity.IsDeleted,
            entity.DeletedAtUtc
        };

        var albumsInCategory = await context.PortfolioAlbums
            .Where(x => x.PortfolioCategoryId == id && !x.IsUserUploaded)
            .Include(x => x.Images)
            .ToListAsync();

        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.IsActive = false;

        foreach (var album in albumsInCategory)
        {
            album.IsDeleted = true;
            album.DeletedAtUtc = now;
            album.IsPublished = false;
            album.AllowClientAccess = false;

            foreach (var image in album.Images)
            {
                image.IsDeleted = true;
                image.DeletedAtUtc = now;
                image.IsPublished = false;
                image.IsCover = false;
            }
        }

        await context.SaveChangesAsync();

        var categories = await context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < categories.Count; i++)
            categories[i].DisplayOrder = i + 1;

        await context.SaveChangesAsync();
        await AuditAsync(
            "SoftDeletePortfolioCategory",
            "PortfolioCategory",
            id.ToString(),
            oldValue,
            new
            {
                DeletedCategoryId = id,
                SoftDeletedAlbumCount = albumsInCategory.Count,
                SoftDeletedImageCount = albumsInCategory.Sum(x => x.Images.Count),
                IsDeleted = true,
                DeletedAtUtc = now
            },
            requestContext);
        await transaction.CommitAsync();

        logger.LogWarning(
            "Admin soft deleted portfolio category. CategoryId: {CategoryId}, Key: {Key}, SoftDeletedAlbumCount: {SoftDeletedAlbumCount}, Admin: {Admin}, TraceId: {TraceId}",
            id,
            oldValue.Key,
            albumsInCategory.Count,
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