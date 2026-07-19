using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPortfolioService(
    AppDbContext context,
    IAuditLogService auditLogService,
    ILogger<AdminPortfolioService> logger) : IAdminPortfolioService
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

    public async Task<ControllerServiceResult> DeleteCategoryAsync(int id, AdminRequestContext requestContext)
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

    public async Task<ControllerServiceResult> DeleteAlbumAsync(int id, AdminRequestContext requestContext)
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

    public async Task<ControllerServiceResult> DeleteImageAsync(int id, AdminRequestContext requestContext)
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
