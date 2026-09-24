namespace DGVisionStudio.Api.Services;

public interface IPrivacyService
{
    Task<GdprExportResponse?> ExportUserDataAsync(string userId);
    Task<bool> AnonymizeUserDataAsync(string userId);
}

public sealed record GdprExportResponse(
    DateTime ExportedAtUtc,
    GdprAccountExport Account,
    IReadOnlyList<GdprOwnedGalleryExport> OwnedGalleries,
    IReadOnlyList<GdprGalleryAccessExport> GalleryAccesses,
    IReadOnlyList<GdprPrintRequestExport> PrintRequests,
    IReadOnlyList<GdprContactRequestExport> ContactRequests);

public sealed record GdprAccountExport(
    string Id,
    string? Email,
    string? UserName,
    string? PhoneNumber,
    DateTime CreatedAtUtc,
    bool IsBlocked);

public sealed record GdprOwnedGalleryExport(
    int Id,
    string Title,
    string Slug,
    string GalleryType,
    string UserGalleryStatus,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    bool IsDeleted,
    int ActiveImageCount);

public sealed record GdprGalleryAccessExport(
    int PortfolioAlbumId,
    string PortfolioAlbumTitle,
    bool PreviewEnabled,
    bool DownloadEnabled,
    DateTime? DownloadExpiresAtUtc);

public sealed record GdprPrintRequestExport(
    int Id,
    int PortfolioAlbumId,
    string FullName,
    string Email,
    string? Phone,
    string? Notes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<GdprPrintRequestItemExport> Items);

public sealed record GdprPrintRequestItemExport(
    int Id,
    int PortfolioImageId,
    int Quantity,
    string Size,
    string? PaperType,
    string? Notes);

public sealed record GdprContactRequestExport(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string Message,
    string Status,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class DeleteAccountRequest
{
    public bool Confirm { get; set; }
}
