using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public sealed record ClientGalleryAdminInput(
    string Title,
    string? TitleEn,
    string? Description,
    string? CoverImageUrl,
    bool IsActive,
    bool IsPublic,
    bool IsPublished,
    int? PortfolioCategoryId,
    GalleryType GalleryType,
    UserClientGalleryStatus UserGalleryStatus,
    List<GalleryUserAccessDto> UserAccesses);

public sealed class ClientGalleryAdminInputNormalizer
{
    public ClientGalleryAdminInput Normalize(AdminCreateClientGalleryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var galleryType = NormalizeGalleryType(request.GalleryType);

        return new ClientGalleryAdminInput(
            request.Title.Trim(),
            NormalizeOptional(request.TitleEn),
            NormalizeOptional(request.Description),
            NormalizeOptional(request.CoverImageUrl),
            request.IsActive,
            request.IsPublic,
            request.IsPublished,
            request.PortfolioCategoryId,
            galleryType,
            NormalizeGalleryStatus(galleryType, request.UserGalleryStatus),
            request.UserAccesses);
    }

    public ClientGalleryAdminInput Normalize(AdminUpdateClientGalleryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var galleryType = NormalizeGalleryType(request.GalleryType);

        return new ClientGalleryAdminInput(
            request.Title.Trim(),
            NormalizeOptional(request.TitleEn),
            NormalizeOptional(request.Description),
            NormalizeOptional(request.CoverImageUrl),
            request.IsActive,
            request.IsPublic,
            request.IsPublished,
            request.PortfolioCategoryId,
            galleryType,
            NormalizeGalleryStatus(galleryType, request.UserGalleryStatus),
            request.UserAccesses);
    }

    private static GalleryType NormalizeGalleryType(GalleryType galleryType) =>
        galleryType switch
        {
            GalleryType.Photoshoot => GalleryType.Photoshoot,
            GalleryType.ClientPrintUpload => GalleryType.ClientPrintUpload,
            _ => GalleryType.Photoshoot
        };

    private static UserClientGalleryStatus NormalizeGalleryStatus(
        GalleryType galleryType,
        UserClientGalleryStatus status)
    {
        if (galleryType == GalleryType.ClientPrintUpload)
        {
            return status switch
            {
                UserClientGalleryStatus.Pending => UserClientGalleryStatus.Pending,
                UserClientGalleryStatus.Processed => UserClientGalleryStatus.Processed,
                UserClientGalleryStatus.Expired => UserClientGalleryStatus.Expired,
                _ => UserClientGalleryStatus.Pending
            };
        }

        return status switch
        {
            UserClientGalleryStatus.PhotoshootUploaded => UserClientGalleryStatus.PhotoshootUploaded,
            UserClientGalleryStatus.PhotoshootInProgress => UserClientGalleryStatus.PhotoshootInProgress,
            UserClientGalleryStatus.PhotoshootReadyForPickup => UserClientGalleryStatus.PhotoshootReadyForPickup,
            UserClientGalleryStatus.PhotoshootCancelled => UserClientGalleryStatus.PhotoshootCancelled,
            _ => UserClientGalleryStatus.PhotoshootUploaded
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
