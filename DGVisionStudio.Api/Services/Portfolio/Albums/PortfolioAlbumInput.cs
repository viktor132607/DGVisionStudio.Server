using DGVisionStudio.Application.DTOs;

namespace DGVisionStudio.Api.Services;

public sealed record PortfolioAlbumInput(
    int PortfolioCategoryId,
    string Slug,
    string Title,
    string? TitleEn,
    string? Description,
    string? CoverImageUrl,
    int DisplayOrder,
    int? ColumnNumber,
    bool IsPublished);

public static class PortfolioAlbumInputNormalizer
{
    public static PortfolioAlbumInput Normalize(CreatePortfolioAlbumRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Normalize(
            model.PortfolioCategoryId,
            model.Slug,
            model.Title,
            model.TitleEn,
            model.Description,
            model.CoverImageUrl,
            model.DisplayOrder,
            model.ColumnNumber,
            model.IsPublished);
    }

    public static PortfolioAlbumInput Normalize(UpdatePortfolioAlbumRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Normalize(
            model.PortfolioCategoryId,
            model.Slug,
            model.Title,
            model.TitleEn,
            model.Description,
            model.CoverImageUrl,
            model.DisplayOrder,
            model.ColumnNumber,
            model.IsPublished);
    }

    private static PortfolioAlbumInput Normalize(
        int portfolioCategoryId,
        string? slug,
        string? title,
        string? titleEn,
        string? description,
        string? coverImageUrl,
        int displayOrder,
        int? columnNumber,
        bool isPublished) =>
        new(
            portfolioCategoryId,
            (slug ?? string.Empty).Trim(),
            (title ?? string.Empty).Trim(),
            NormalizeOptional(titleEn),
            NormalizeOptional(description),
            NormalizeOptional(coverImageUrl),
            displayOrder,
            columnNumber,
            isPublished);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
