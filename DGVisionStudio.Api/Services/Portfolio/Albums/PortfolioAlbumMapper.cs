using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumMapper
{
    public PortfolioAlbum CreateEntity(PortfolioAlbumInput input) =>
        new()
        {
            PortfolioCategoryId = input.PortfolioCategoryId,
            Slug = input.Slug,
            Title = input.Title,
            TitleEn = input.TitleEn,
            Description = input.Description,
            CoverImageUrl = input.CoverImageUrl,
            DisplayOrder = input.DisplayOrder,
            ColumnNumber = input.ColumnNumber,
            IsPublished = input.IsPublished,
            IsUserUploaded = false,
            IsSeenByAdmin = true,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };

    public void Apply(PortfolioAlbum entity, PortfolioAlbumInput input)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.PortfolioCategoryId = input.PortfolioCategoryId;
        entity.Slug = input.Slug;
        entity.Title = input.Title;
        entity.TitleEn = input.TitleEn;
        entity.Description = input.Description;
        entity.CoverImageUrl = input.CoverImageUrl;
        entity.DisplayOrder = input.DisplayOrder;
        entity.ColumnNumber = input.ColumnNumber;
        entity.IsPublished = input.IsPublished;
    }

    public PortfolioAlbumSnapshot Snapshot(PortfolioAlbum entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PortfolioAlbumSnapshot(
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
            entity.DeletedAtUtc);
    }
}

public sealed record PortfolioAlbumSnapshot(
    int Id,
    int PortfolioCategoryId,
    string Slug,
    string Title,
    string? TitleEn,
    string? Description,
    string? CoverImageUrl,
    int DisplayOrder,
    int? ColumnNumber,
    bool IsPublished,
    bool IsDeleted,
    DateTime? DeletedAtUtc);
