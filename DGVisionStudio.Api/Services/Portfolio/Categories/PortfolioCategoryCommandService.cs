using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryCommandService(
    AppDbContext context,
    PortfolioCategoryAuditService auditService,
    PortfolioCategoryOrderingService orderingService,
    ILogger<PortfolioCategoryCommandService> logger)
{
    public async Task<ControllerServiceResult> CreateCategoryAsync(
        CreatePortfolioCategoryRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var normalized = Normalize(model.Key, model.Name, model.NameEn, model.Description, model.DisplayOrder);
        var validationError = Validate(normalized);
        if (validationError != null)
            return validationError;

        if (await KeyExistsAsync(normalized.Key))
            return DuplicateKey();

        var entity = new PortfolioCategory
        {
            Key = normalized.Key,
            Name = normalized.Name,
            NameEn = normalized.NameEn,
            Description = normalized.Description,
            DisplayOrder = normalized.DisplayOrder,
            IsActive = model.IsActive,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        context.PortfolioCategories.Add(entity);
        await context.SaveChangesAsync();
        await auditService.LogAsync(
            "CreatePortfolioCategory",
            entity.Id.ToString(),
            null,
            entity,
            requestContext);
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

        var normalized = Normalize(model.Key, model.Name, model.NameEn, model.Description, model.DisplayOrder);
        var validationError = Validate(normalized);
        if (validationError != null)
            return validationError;

        if (await KeyExistsAsync(normalized.Key, id))
            return DuplicateKey();

        var oldValue = Snapshot(entity);

        entity.Key = normalized.Key;
        entity.Name = normalized.Name;
        entity.NameEn = normalized.NameEn;
        entity.Description = normalized.Description;
        entity.DisplayOrder = normalized.DisplayOrder;
        entity.IsActive = model.IsActive;

        await context.SaveChangesAsync();
        await auditService.LogAsync(
            "UpdatePortfolioCategory",
            entity.Id.ToString(),
            oldValue,
            Snapshot(entity),
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

    public async Task<ControllerServiceResult> DeleteCategoryAsync(
        int id,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var entity = await context.PortfolioCategories
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var now = DateTime.UtcNow;
        var oldValue = Snapshot(entity);

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
        await orderingService.NormalizeDisplayOrderAsync();

        await auditService.LogAsync(
            "SoftDeletePortfolioCategory",
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

    private async Task<bool> KeyExistsAsync(string key, int? excludedId = null)
    {
        var query = context.PortfolioCategories
            .AsNoTracking()
            .Where(x => x.Key.ToLower() == key);

        if (excludedId.HasValue)
            query = query.Where(x => x.Id != excludedId.Value);

        return await query.AnyAsync();
    }

    private static ControllerServiceResult? Validate(NormalizedCategoryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return ControllerServiceResult.BadRequest(new { message = "Името на български е задължително." });

        if (string.IsNullOrWhiteSpace(input.NameEn))
            return ControllerServiceResult.BadRequest(new { message = "Името на английски е задължително." });

        if (string.IsNullOrWhiteSpace(input.Key))
            return ControllerServiceResult.BadRequest(new { message = "Ключът е задължителен." });

        return null;
    }

    private static ControllerServiceResult DuplicateKey() =>
        ControllerServiceResult.BadRequest(new { message = "Вече съществува категория със същия ключ." });

    private static NormalizedCategoryInput Normalize(
        string? key,
        string? name,
        string? nameEn,
        string? description,
        int displayOrder) =>
        new(
            (key ?? string.Empty).Trim().ToLowerInvariant(),
            (name ?? string.Empty).Trim(),
            (nameEn ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            displayOrder < 1 ? 1 : displayOrder);

    private static CategorySnapshot Snapshot(PortfolioCategory entity) =>
        new(
            entity.Id,
            entity.Key,
            entity.Name,
            entity.NameEn,
            entity.Description,
            entity.DisplayOrder,
            entity.IsActive,
            entity.IsDeleted,
            entity.DeletedAtUtc);

    private sealed record NormalizedCategoryInput(
        string Key,
        string Name,
        string NameEn,
        string? Description,
        int DisplayOrder);

    private sealed record CategorySnapshot(
        int Id,
        string Key,
        string Name,
        string NameEn,
        string? Description,
        int DisplayOrder,
        bool IsActive,
        bool IsDeleted,
        DateTime? DeletedAtUtc);
}
