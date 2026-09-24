using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PrivacyExportQueryService(
    AppDbContext context,
    PrivacyExportMapper mapper)
{
    public async Task<GdprExportResponse?> ExportUserDataAsync(string userId)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
            return null;

        var email = user.Email ?? string.Empty;

        var ownedGalleries = await context.PortfolioAlbums
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Images)
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var galleryAccesses = await context.UserAlbumAccesses
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.PortfolioAlbumId)
            .ToListAsync();

        var printRequests = await context.PrintRequests
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var contactRequests = await context.ContactRequests
            .AsNoTracking()
            .Where(x => x.Email == email)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return new GdprExportResponse(
            DateTime.UtcNow,
            mapper.MapAccount(user),
            ownedGalleries.Select(mapper.MapOwnedGallery).ToList(),
            galleryAccesses.Select(mapper.MapGalleryAccess).ToList(),
            printRequests.Select(mapper.MapPrintRequest).ToList(),
            contactRequests.Select(mapper.MapContactRequest).ToList());
    }
}
