using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/portfolio")]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _context;
    private const int HomeSlideshowLimit = 10;

    public PortfolioController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var items = await _context.PortfolioCategories
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums([FromQuery] int? categoryId = null)
    {
        var query = _context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Where(x =>
                x.IsPublished &&
                x.IsActive &&
                x.PortfolioCategory != null &&
                x.PortfolioCategory.IsActive)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(x => x.PortfolioCategoryId == categoryId.Value);

        var items = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PortfolioCategoryId,
                categoryName = x.PortfolioCategory != null ? x.PortfolioCategory.Name : null,
                categoryNameEn = x.PortfolioCategory != null ? x.PortfolioCategory.NameEn : null,
                x.Slug,
                x.Title,
                x.TitleEn,
                x.Description,
                x.CoverImageUrl,
                x.DisplayOrder,
                x.ColumnNumber,
                x.IsPublished,
                x.IsActive,
                x.AllowClientAccess,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("albums/{slug}")]
    public async Task<IActionResult> GetAlbum(string slug)
    {
        var album = await _context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.Images.Where(i => i.IsPublished).OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync(x =>
                x.Slug == slug &&
                x.IsPublished &&
                x.IsActive &&
                x.PortfolioCategory != null &&
                x.PortfolioCategory.IsActive);

        if (album is null)
            return NotFound();

        return Ok(new
        {
            album.Id,
            album.PortfolioCategoryId,
            categoryName = album.PortfolioCategory != null ? album.PortfolioCategory.Name : null,
            categoryNameEn = album.PortfolioCategory != null ? album.PortfolioCategory.NameEn : null,
            album.Slug,
            album.Title,
            album.TitleEn,
            album.Description,
            album.CoverImageUrl,
            album.DisplayOrder,
            album.ColumnNumber,
            album.IsPublished,
            album.IsActive,
            album.AllowClientAccess,
            album.CreatedAtUtc,
            Images = album.Images
                .Where(i => i.IsPublished)
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.Id)
                .Select(i => new
                {
                    i.Id,
                    i.PortfolioAlbumId,
                    i.ImageUrl,
                    i.ThumbnailUrl,
                    i.AltText,
                    i.Caption,
                    i.Width,
                    i.Height,
                    i.DisplayOrder,
                    i.IsCover,
                    i.IsPublished,
                    i.CreatedAtUtc
                })
                .ToList()
        });
    }

    [HttpGet("images")]
    public async Task<IActionResult> GetImages([FromQuery] int? albumId = null)
    {
        var query = _context.PortfolioImages
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
            .ThenInclude(x => x.PortfolioCategory)
            .Where(x =>
                x.IsPublished &&
                x.PortfolioAlbum != null &&
                x.PortfolioAlbum.IsPublished &&
                x.PortfolioAlbum.IsActive &&
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
                x.AltText,
                x.Caption,
                x.DisplayOrder,
                x.IsPublished,
                x.PortfolioAlbumId,
                albumTitle = x.PortfolioAlbum != null ? x.PortfolioAlbum.Title : null,
                albumIsActive = x.PortfolioAlbum != null ? x.PortfolioAlbum.IsActive : false,
                categoryName = x.PortfolioAlbum != null && x.PortfolioAlbum.PortfolioCategory != null
                    ? x.PortfolioAlbum.PortfolioCategory.Name
                    : null,
                categoryNameEn = x.PortfolioAlbum != null && x.PortfolioAlbum.PortfolioCategory != null
                    ? x.PortfolioAlbum.PortfolioCategory.NameEn
                    : null
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("home-slideshow")]
    public async Task<IActionResult> GetHomeSlideshow()
    {
        var items = await _context.PortfolioImages
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
            .ThenInclude(x => x.PortfolioCategory)
            .Where(x =>
                x.IsPublished &&
                x.PortfolioAlbum != null &&
                x.PortfolioAlbum.IsPublished &&
                x.PortfolioAlbum.IsActive &&
                x.PortfolioAlbum.PortfolioCategory != null &&
                x.PortfolioAlbum.PortfolioCategory.IsActive &&
                (!string.IsNullOrEmpty(x.ThumbnailUrl) || !string.IsNullOrEmpty(x.ImageUrl)))
            .OrderBy(x => Guid.NewGuid())
            .Take(HomeSlideshowLimit)
            .Select(x => new
            {
                x.Id,
                x.ImageUrl,
                x.ThumbnailUrl,
                x.AltText,
                x.Caption,
                x.DisplayOrder,
                x.IsPublished,
                x.PortfolioAlbumId,
                albumTitle = x.PortfolioAlbum != null ? x.PortfolioAlbum.Title : null,
                albumIsActive = x.PortfolioAlbum != null ? x.PortfolioAlbum.IsActive : false,
                categoryName = x.PortfolioAlbum != null && x.PortfolioAlbum.PortfolioCategory != null
                    ? x.PortfolioAlbum.PortfolioCategory.Name
                    : null,
                categoryNameEn = x.PortfolioAlbum != null && x.PortfolioAlbum.PortfolioCategory != null
                    ? x.PortfolioAlbum.PortfolioCategory.NameEn
                    : null
            })
            .ToListAsync();

        return Ok(items);
    }
}