using DGVisionStudio.Application.DTOs;

namespace DGVisionStudio.Api.Services;

public sealed record PortfolioImageInput(
    int PortfolioAlbumId,
    string ImageUrl,
    string? ThumbnailUrl,
    string? AltText,
    string? Caption,
    int Width,
    int Height,
    int DisplayOrder,
    bool IsCover,
    bool IsPublished);

public static class PortfolioImageInputNormalizer
{
    public static PortfolioImageInput Normalize(CreatePortfolioImageRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Normalize(
            model.PortfolioAlbumId,
            model.ImageUrl,
            model.ThumbnailUrl,
            model.AltText,
            model.Caption,
            model.Width,
            model.Height,
            model.DisplayOrder,
            model.IsCover,
            model.IsPublished);
    }

    public static PortfolioImageInput Normalize(UpdatePortfolioImageRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Normalize(
            model.PortfolioAlbumId,
            model.ImageUrl,
            model.ThumbnailUrl,
            model.AltText,
            model.Caption,
            model.Width,
            model.Height,
            model.DisplayOrder,
            model.IsCover,
            model.IsPublished);
    }

    private static PortfolioImageInput Normalize(
        int portfolioAlbumId,
        string? imageUrl,
        string? thumbnailUrl,
        string? altText,
        string? caption,
        int? width,
        int? height,
        int displayOrder,
        bool isCover,
        bool isPublished) =>
        new(
            portfolioAlbumId,
            (imageUrl ?? string.Empty).Trim(),
            NormalizeOptional(thumbnailUrl),
            NormalizeOptional(altText),
            NormalizeOptional(caption),
            width ?? 0,
            height ?? 0,
            displayOrder,
            isCover,
            isPublished);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
