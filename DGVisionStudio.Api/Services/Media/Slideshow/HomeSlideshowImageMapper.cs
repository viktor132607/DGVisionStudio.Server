using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowImageMapper
{
    public SlideshowImageDto Map(
        PortfolioImage image,
        bool isSelected = false,
        int? slideshowOrder = null) =>
        new()
        {
            Id = image.Id,
            ImageUrl = image.ImageUrl,
            ThumbnailUrl = image.ThumbnailUrl,
            AltText = image.AltText,
            Caption = image.Caption,
            DisplayOrder = image.DisplayOrder,
            IsPublished = image.IsPublished,
            PortfolioAlbumId = image.PortfolioAlbumId,
            AlbumTitle = image.PortfolioAlbum?.Title,
            CategoryName =
                image.PortfolioAlbum?
                    .PortfolioCategory?
                    .Name,
            CategoryNameEn =
                image.PortfolioAlbum?
                    .PortfolioCategory?
                    .NameEn,
            IsSelected = isSelected,
            SlideshowOrder = slideshowOrder
        };
}
