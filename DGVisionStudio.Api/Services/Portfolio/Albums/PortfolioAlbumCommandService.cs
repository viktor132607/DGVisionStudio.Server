using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumCommandService(
    AppDbContext context,
    PortfolioAlbumInputValidator validator,
    PortfolioAlbumMapper mapper,
    PortfolioAlbumAuditService auditService,
    ILogger<PortfolioAlbumCommandService> logger)
{
    public async Task<ControllerServiceResult> CreateAlbumAsync(
        CreatePortfolioAlbumRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var input = PortfolioAlbumInputNormalizer.Normalize(model);
        var validationError = await validator.ValidateAsync(input);
        if (validationError != null)
            return validationError;

        var entity = mapper.CreateEntity(input);

        context.PortfolioAlbums.Add(entity);
        await context.SaveChangesAsync();

        await auditService.LogAsync(
            "CreatePortfolioAlbum",
            entity.Id.ToString(),
            null,
            entity,
            requestContext);

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

        var entity = await context.PortfolioAlbums
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsUserUploaded);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var oldValue = mapper.Snapshot(entity);
        var input = PortfolioAlbumInputNormalizer.Normalize(model);
        var validationError = await validator.ValidateAsync(input, id);
        if (validationError != null)
            return validationError;

        mapper.Apply(entity, input);

        await context.SaveChangesAsync();

        await auditService.LogAsync(
            "UpdatePortfolioAlbum",
            entity.Id.ToString(),
            oldValue,
            mapper.Snapshot(entity),
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
        var oldValue = mapper.Snapshot(entity);

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

        await auditService.LogAsync(
            "SoftDeletePortfolioAlbum",
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
}
