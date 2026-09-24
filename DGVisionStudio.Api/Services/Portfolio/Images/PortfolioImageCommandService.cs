using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioImageCommandService(
    AppDbContext context,
    PortfolioImageInputValidator validator,
    PortfolioImageMapper mapper,
    PortfolioImageAuditService auditService,
    ILogger<PortfolioImageCommandService> logger)
{
    public async Task<ControllerServiceResult> CreateImageAsync(
        CreatePortfolioImageRequest model,
        AdminRequestContext requestContext)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var input = PortfolioImageInputNormalizer.Normalize(model);
        var validationError = await validator.ValidateAsync(input);
        if (validationError != null)
            return validationError;

        var entity = mapper.CreateEntity(input);

        context.PortfolioImages.Add(entity);
        await context.SaveChangesAsync();

        await auditService.LogAsync(
            "CreatePortfolioImage",
            entity.Id.ToString(),
            null,
            entity,
            requestContext);

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
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PortfolioAlbum != null &&
                !x.PortfolioAlbum.IsUserUploaded);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var oldValue = mapper.Snapshot(entity);
        var input = PortfolioImageInputNormalizer.Normalize(model);
        var validationError = await validator.ValidateAsync(input);
        if (validationError != null)
            return validationError;

        mapper.Apply(entity, input);

        await context.SaveChangesAsync();

        await auditService.LogAsync(
            "UpdatePortfolioImage",
            entity.Id.ToString(),
            oldValue,
            mapper.Snapshot(entity),
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
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PortfolioAlbum != null &&
                !x.PortfolioAlbum.IsUserUploaded);

        if (entity == null)
            return ControllerServiceResult.NotFound();

        var now = DateTime.UtcNow;
        var oldValue = mapper.Snapshot(entity);

        entity.IsDeleted = true;
        entity.DeletedAtUtc = now;
        entity.IsPublished = false;
        entity.IsCover = false;

        await context.SaveChangesAsync();

        await auditService.LogAsync(
            "SoftDeletePortfolioImage",
            id.ToString(),
            oldValue,
            new
            {
                DeletedImageId = id,
                IsDeleted = true,
                DeletedAtUtc = now
            },
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
}
