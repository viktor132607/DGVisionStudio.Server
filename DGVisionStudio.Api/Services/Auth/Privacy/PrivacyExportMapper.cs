using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class PrivacyExportMapper
{
    public GdprAccountExport MapAccount(ApplicationUser user) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAtUtc,
            user.IsBlocked);

    public GdprOwnedGalleryExport MapOwnedGallery(PortfolioAlbum album) =>
        new(
            album.Id,
            album.Title,
            album.Slug,
            album.GalleryType.ToString(),
            album.UserGalleryStatus.ToString(),
            album.CreatedAtUtc,
            album.ExpiresAtUtc,
            album.IsDeleted,
            album.Images.Count(image => !image.IsDeleted));

    public GdprGalleryAccessExport MapGalleryAccess(UserAlbumAccess access) =>
        new(
            access.PortfolioAlbumId,
            access.PortfolioAlbum.Title,
            access.PreviewEnabled,
            access.DownloadEnabled,
            access.DownloadExpiresAtUtc);

    public GdprPrintRequestExport MapPrintRequest(PrintRequest request) =>
        new(
            request.Id,
            request.PortfolioAlbumId,
            request.FullName,
            request.Email,
            request.Phone,
            request.Notes,
            request.Status,
            request.CreatedAtUtc,
            request.UpdatedAtUtc,
            request.Items
                .Select(MapPrintRequestItem)
                .ToList());

    public GdprContactRequestExport MapContactRequest(ContactRequest request) =>
        new(
            request.Id,
            request.Name,
            request.Email,
            request.Phone,
            request.Subject,
            request.Message,
            request.Status.ToString(),
            request.IsArchived,
            request.CreatedAtUtc,
            request.UpdatedAtUtc);

    private static GdprPrintRequestItemExport MapPrintRequestItem(
        PrintRequestItem item) =>
        new(
            item.Id,
            item.PortfolioImageId,
            item.Quantity,
            item.Size,
            item.PaperType,
            item.Notes);
}
