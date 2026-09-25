using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed class ClientGalleryPhotoMapper
{
    public ClientPhotoDto Map(
        PortfolioImage image,
        bool canDownload,
        int galleryId)
    {
        var isVideo = IsVideoPath(image.ImageUrl);
        var mediaType = isVideo ? "Video" : "Image";
        var previewUrl = isVideo
            ? image.ImageUrl
            : string.IsNullOrWhiteSpace(image.ThumbnailUrl)
                ? image.ImageUrl
                : image.ThumbnailUrl!;

        return new ClientPhotoDto
        {
            Id = image.Id,
            PreviewUrl = previewUrl,
            OriginalUrl = isVideo ? image.ImageUrl : null,
            DownloadUrl = canDownload
                ? $"/api/client-galleries/{galleryId}/photos/{image.Id}/download"
                : null,
            Name = image.Name,
            AltText = image.AltText,
            Caption = image.Caption,
            CanDownload =
                canDownload &&
                !string.IsNullOrWhiteSpace(image.ImageUrl),
            DisplayOrder = image.DisplayOrder,
            Description = image.Caption,
            IsPublished = image.IsPublished,
            MediaType = mediaType,
            ContentType = isVideo
                ? GetVideoContentType(image.ImageUrl)
                : null,
            ShowInPublicGallery = false,
            VisibleToAllAuthorizedUsers = true,
            AllowedUserIds = new List<string>()
        };
    }

    private static bool IsVideoPath(string? value)
    {
        var extension = Path
            .GetExtension((value ?? string.Empty).Split('?', '#')[0])
            .ToLowerInvariant();

        return extension is ".mp4" or ".mov" or ".webm" or ".m4v";
    }

    private static string? GetVideoContentType(string? value)
    {
        var extension = Path
            .GetExtension((value ?? string.Empty).Split('?', '#')[0])
            .ToLowerInvariant();

        return extension switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            ".m4v" => "video/x-m4v",
            _ => null
        };
    }
}
