using System.Data;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed record AlbumSelectionRequest(int[]? AlbumIds);
public sealed record AlbumMoveRequest(int[]? AlbumIds, int CategoryId);

public sealed class PortfolioAlbumBulkService(AppDbContext db, IAuditLogService audit)
{
    public async Task<ControllerServiceResult> ExecuteAsync(
        int[]? albumIds, int? categoryId, AdminRequestContext admin, CancellationToken token)
    {
        if (albumIds is null || albumIds.Length == 0 || albumIds.Any(id => id <= 0))
            return ControllerServiceResult.BadRequest(new { message = "Маркирай поне един валиден албум." });

        var ids = albumIds.Distinct().ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        if (categoryId is not null && !await db.PortfolioCategories.AnyAsync(c => c.Id == categoryId && c.IsActive, token))
            return ControllerServiceResult.BadRequest(new { message = "Избери съществуваща активна категория." });

        var albums = await db.PortfolioAlbums.Include(a => a.Images)
            .Where(a => ids.Contains(a.Id) && !a.IsUserUploaded)
            .OrderBy(a => a.PortfolioCategoryId).ThenBy(a => a.DisplayOrder).ThenBy(a => a.Id).ToListAsync(token);
        if (albums.Count != ids.Length)
            return ControllerServiceResult.BadRequest(new { message = "Някои избрани албуми вече не съществуват. Обнови списъка. Няма направени промени." });

        var before = albums.Select(a => new { a.Id, a.PortfolioCategoryId, a.DisplayOrder, a.IsPublished, a.AllowClientAccess }).ToArray();
        var now = DateTime.UtcNow;
        var order = categoryId is null ? 0 : await db.PortfolioAlbums
            .Where(a => a.PortfolioCategoryId == categoryId).Select(a => (int?)a.DisplayOrder).MaxAsync(token) ?? 0;
        foreach (var album in albums)
        {
            if (categoryId is not null)
            {
                if (album.PortfolioCategoryId == categoryId) continue;
                album.PortfolioCategoryId = categoryId.Value;
                album.DisplayOrder = checked(++order);
            }
            else
            {
                album.IsDeleted = true;
                album.DeletedAtUtc = now;
                album.IsPublished = false;
                album.AllowClientAccess = false;
                foreach (var image in album.Images)
                {
                    image.IsDeleted = true;
                    image.DeletedAtUtc = now;
                    image.IsPublished = false;
                    image.IsCover = false;
                }
            }
        }

        await db.SaveChangesAsync(token);
        await audit.LogAsync(admin.UserId, admin.Email,
            categoryId is null ? "BulkSoftDeletePortfolioAlbums" : "BulkMovePortfolioAlbums",
            "PortfolioAlbum", null, before,
            new { AlbumIds = ids, CategoryId = categoryId, IsDeleted = categoryId is null },
            admin.RemoteIpAddress, admin.UserAgent, admin.TraceId, token);
        await transaction.CommitAsync(token);
        return ControllerServiceResult.Ok(new { albumIds = ids, categoryId, count = ids.Length });
    }
}
