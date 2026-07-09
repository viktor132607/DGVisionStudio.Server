using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public interface IPrivacyService
{
    Task<GdprExportResponse?> ExportUserDataAsync(string userId);
    Task<bool> AnonymizeUserDataAsync(string userId);
}

public sealed class PrivacyService(AppDbContext context) : IPrivacyService
{
    public async Task<GdprExportResponse?> ExportUserDataAsync(string userId)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
            return null;

        var email = user.Email ?? string.Empty;

        var ownedGalleries = await context.PortfolioAlbums
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new GdprOwnedGalleryExport(
                x.Id,
                x.Title,
                x.Slug,
                x.GalleryType.ToString(),
                x.UserGalleryStatus.ToString(),
                x.CreatedAtUtc,
                x.ExpiresAtUtc,
                x.IsDeleted,
                x.Images.Count(i => !i.IsDeleted)))
            .ToListAsync();

        var galleryAccesses = await context.UserAlbumAccesses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.PortfolioAlbumId)
            .Select(x => new GdprGalleryAccessExport(
                x.PortfolioAlbumId,
                x.PortfolioAlbum.Title,
                x.PreviewEnabled,
                x.DownloadEnabled,
                x.DownloadExpiresAtUtc))
            .ToListAsync();

        var printRequests = await context.PrintRequests
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new GdprPrintRequestExport(
                x.Id,
                x.PortfolioAlbumId,
                x.FullName,
                x.Email,
                x.Phone,
                x.Notes,
                x.Status,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.Items.Select(item => new GdprPrintRequestItemExport(
                    item.Id,
                    item.PortfolioImageId,
                    item.Quantity,
                    item.Size,
                    item.PaperType,
                    item.Notes)).ToList()))
            .ToListAsync();

        var contactRequests = await context.ContactRequests
            .AsNoTracking()
            .Where(x => x.Email == email)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new GdprContactRequestExport(
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.Subject,
                x.Message,
                x.Status.ToString(),
                x.IsArchived,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync();

        return new GdprExportResponse(
            DateTime.UtcNow,
            new GdprAccountExport(
                user.Id,
                user.Email,
                user.UserName,
                user.PhoneNumber,
                user.CreatedAtUtc,
                user.IsBlocked),
            ownedGalleries,
            galleryAccesses,
            printRequests,
            contactRequests);
    }

    public async Task<bool> AnonymizeUserDataAsync(string userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
            return false;

        var oldEmail = user.Email;
        var anonymizedEmail = $"deleted-user-{Guid.NewGuid():N}@deleted.local";
        var normalizedAnonymizedEmail = anonymizedEmail.ToUpperInvariant();

        user.UserName = anonymizedEmail;
        user.NormalizedUserName = normalizedAnonymizedEmail;
        user.Email = anonymizedEmail;
        user.NormalizedEmail = normalizedAnonymizedEmail;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        user.TwoFactorEnabled = false;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.IsBlocked = true;
        user.IsSeenByAdmin = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        var ownedGalleries = await context.PortfolioAlbums
            .IgnoreQueryFilters()
            .Where(x => x.OwnerUserId == userId)
            .ToListAsync();

        foreach (var gallery in ownedGalleries)
            gallery.OwnerUserId = null;

        var accesses = await context.UserAlbumAccesses
            .Where(x => x.UserId == userId)
            .ToListAsync();
        context.UserAlbumAccesses.RemoveRange(accesses);

        var printRequests = await context.PrintRequests
            .Where(x => x.UserId == userId)
            .ToListAsync();

        foreach (var request in printRequests)
        {
            request.FullName = "Deleted user";
            request.Email = anonymizedEmail;
            request.Phone = null;
            request.Notes = null;
            request.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(oldEmail))
        {
            var contactRequests = await context.ContactRequests
                .Where(x => x.Email == oldEmail)
                .ToListAsync();

            foreach (var request in contactRequests)
            {
                request.Name = "Deleted user";
                request.Email = anonymizedEmail;
                request.Phone = null;
                request.Subject = null;
                request.Message = "Deleted by GDPR request.";
                request.AdminComment = null;
                request.IsArchived = true;
                request.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        return true;
    }
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
