using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioQueryService : IPortfolioQueryService
{
    private readonly AppDbContext _context;

    public PortfolioQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetCategoriesAsync() =>
        ControllerServiceResult.Ok(await _context.PortfolioCategories
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetAlbumsAsync(int? categoryId)
    {
        var query = _context.PortfolioAlbums
            .Include(x => x.PortfolioCategory)
            .Where(x =>
                x.IsPublished &&
                !x.IsUserUploaded &&
                x.PortfolioCategory != null &&
                x.PortfolioCategory.IsActive)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(x => x.PortfolioCategoryId == categoryId.Value);

        return ControllerServiceResult.Ok(await query
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync());
    }

    public async Task<ControllerServiceResult> GetAlbumAsync(string slug)
    {
        var album = await _context.PortfolioAlbums
            .Include(x => x.PortfolioCategory)
            .Include(x => x.Images.Where(i => i.IsPublished).OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync(x =>
                x.Slug == slug &&
                x.IsPublished &&
                !x.IsUserUploaded &&
                x.PortfolioCategory != null &&
                x.PortfolioCategory.IsActive);

        return album is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(album);
    }

    public async Task<ControllerServiceResult> GetImagesAsync(int? albumId)
    {
        var query = _context.PortfolioImages
            .Include(x => x.PortfolioAlbum!)
            .ThenInclude(x => x.PortfolioCategory)
            .Where(x =>
                x.IsPublished &&
                x.PortfolioAlbum != null &&
                x.PortfolioAlbum.IsPublished &&
                !x.PortfolioAlbum.IsUserUploaded &&
                x.PortfolioAlbum.PortfolioCategory != null &&
                x.PortfolioAlbum.PortfolioCategory.IsActive)
            .AsQueryable();

        if (albumId.HasValue)
            query = query.Where(x => x.PortfolioAlbumId == albumId.Value);

        var items = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ImageUrl,
                x.ThumbnailUrl,
                x.Name,
                x.AltText,
                x.Caption,
                mediaType = IsVideoPath(x.ImageUrl) ? "Video" : "Image",
                contentType = GetContentType(x.ImageUrl),
                x.DisplayOrder,
                x.IsPublished,
                x.PortfolioAlbumId,
                albumTitle = x.PortfolioAlbum != null ? x.PortfolioAlbum.Title : null,
                categoryName = x.PortfolioAlbum != null && x.PortfolioAlbum.PortfolioCategory != null
                    ? x.PortfolioAlbum.PortfolioCategory.Name
                    : null,
                categoryNameEn = x.PortfolioAlbum != null && x.PortfolioAlbum.PortfolioCategory != null
                    ? x.PortfolioAlbum.PortfolioCategory.NameEn
                    : null
            })
            .ToListAsync();

        return ControllerServiceResult.Ok(items);
    }

    private static bool IsVideoPath(string? value)
    {
        var extension = Path.GetExtension((value ?? string.Empty).Split('?', '#')[0]).ToLowerInvariant();
        return extension is ".mp4" or ".mov" or ".webm" or ".m4v";
    }

    private static string? GetContentType(string? value)
    {
        var extension = Path.GetExtension((value ?? string.Empty).Split('?', '#')[0]).ToLowerInvariant();
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
