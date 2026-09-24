using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioImageMapper
{
    public PortfolioImage CreateEntity(PortfolioImageInput input) =>
        new()
        {
            PortfolioAlbumId = input.PortfolioAlbumId,
            ImageUrl = input.ImageUrl,
            ThumbnailUrl = input.ThumbnailUrl,
            AltText = input.AltText,
            Caption = input.Caption,
            Width = input.Width,
            Height = input.Height,
            DisplayOrder = input.DisplayOrder,
            IsCover = input.IsCover,
            IsPublished = input.IsPublished,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };

    public void Apply(PortfolioImage entity, PortfolioImageInput input)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.PortfolioAlbumId = input.PortfolioAlbumId;
        entity.ImageUrl = input.ImageUrl;
        entity.ThumbnailUrl = input.ThumbnailUrl;
        entity.AltText = input.AltText;
        entity.Caption = input.Caption;
        entity.Width = input.Width;
        entity.Height = input.Height;
        entity.DisplayOrder = input.DisplayOrder;
        entity.IsCover = input.IsCover;
        entity.IsPublished = input.IsPublished;
    }

    public PortfolioImageSnapshot Snapshot(PortfolioImage entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PortfolioImageSnapshot(
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
            entity.DeletedAtUtc);
    }
}

public sealed record PortfolioImageSnapshot(
    int Id,
    int PortfolioAlbumId,
    string ImageUrl,
    string? ThumbnailUrl,
    string? AltText,
    string? Caption,
    int Width,
    int Height,
    int DisplayOrder,
    bool IsCover,
    bool IsPublished,
    bool IsDeleted,
    DateTime? DeletedAtUtc);
