using DGVisionStudio.Infrastructure.DTOs;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/portfolio")]
public class AdminPortfolioController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminPortfolioController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var items = await _context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("categories/{id:int}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var entity = await _context.PortfolioCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
            return NotFound();

        return Ok(entity);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreatePortfolioCategoryRequest model)
    {
        var key = (model.Key ?? string.Empty).Trim().ToLowerInvariant();
        var name = (model.Name ?? string.Empty).Trim();
        var nameEn = (model.NameEn ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Името на български е задължително." });

        if (string.IsNullOrWhiteSpace(nameEn))
            return BadRequest(new { message = "Името на английски е задължително." });

        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "Ключът е задължителен." });

        var duplicateKeyExists = await _context.PortfolioCategories
            .AnyAsync(x => x.Key.ToLower() == key);

        if (duplicateKeyExists)
            return BadRequest(new { message = "Вече съществува категория със същия ключ." });

        var entity = new PortfolioCategory
        {
            Key = key,
            Name = name,
            NameEn = nameEn,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            DisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder,
            IsActive = model.IsActive
        };

        _context.PortfolioCategories.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(entity);
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdatePortfolioCategoryRequest model)
    {
        var entity = await _context.PortfolioCategories.FindAsync(id);
        if (entity == null)
            return NotFound();

        var key = (model.Key ?? string.Empty).Trim().ToLowerInvariant();
        var name = (model.Name ?? string.Empty).Trim();
        var nameEn = (model.NameEn ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Името на български е задължително." });

        if (string.IsNullOrWhiteSpace(nameEn))
            return BadRequest(new { message = "Името на английски е задължително." });

        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "Ключът е задължителен." });

        var duplicateKeyExists = await _context.PortfolioCategories
            .AnyAsync(x => x.Id != id && x.Key.ToLower() == key);

        if (duplicateKeyExists)
            return BadRequest(new { message = "Вече съществува категория със същия ключ." });

        entity.Key = key;
        entity.Name = name;
        entity.NameEn = nameEn;
        entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        entity.DisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder;
        entity.IsActive = model.IsActive;

        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("categories/{id:int}/move")]
    public async Task<IActionResult> MoveCategory(int id, [FromBody] MovePortfolioCategoryRequest model)
    {
        var entity = await _context.PortfolioCategories.FindAsync(id);
        if (entity == null)
            return NotFound();

        var categories = await _context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var source = categories.FirstOrDefault(x => x.Id == id);
        if (source == null)
            return NotFound();

        categories.Remove(source);

        var requestedDisplayOrder = model.DisplayOrder < 1 ? 1 : model.DisplayOrder;
        var targetIndex = Math.Min(requestedDisplayOrder - 1, categories.Count);
        categories.Insert(targetIndex, source);

        for (var i = 0; i < categories.Count; i++)
        {
            categories[i].DisplayOrder = i + 1;
        }

        await _context.SaveChangesAsync();

        return Ok(categories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id));
    }

    [HttpGet("categories/{id:int}/albums")]
    public async Task<IActionResult> GetCategoryAlbums(int id)
    {
        var category = await _context.PortfolioCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (category == null)
            return NotFound();

        var albums = await _context.PortfolioAlbums
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.TitleEn,
                x.Slug,
                x.Description,
                x.CoverImageUrl,
                x.DisplayOrder,
                x.ColumnNumber,
                x.IsPublished,
                x.IsActive,
                x.AllowClientAccess,
                x.PortfolioCategoryId,
                x.CreatedAtUtc,
                IsSelected = x.PortfolioCategoryId == id
            })
            .ToListAsync();

        return Ok(new { category, albums });
    }

    [HttpPut("categories/{id:int}/albums")]
    public async Task<IActionResult> UpdateCategoryAlbums(int id, [FromBody] UpdateCategoryAlbumsRequest model)
    {
        var categoryExists = await _context.PortfolioCategories.AnyAsync(x => x.Id == id);
        if (!categoryExists)
            return NotFound();

        var selectedAlbumIds = (model.AlbumIds ?? new List<int>())
            .Distinct()
            .ToHashSet();

        var albums = await _context.PortfolioAlbums.ToListAsync();

        foreach (var album in albums)
        {
            if (selectedAlbumIds.Contains(album.Id))
            {
                album.PortfolioCategoryId = id;
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var entity = await _context.PortfolioCategories.FindAsync(id);
        if (entity == null)
            return NotFound();

        var fallbackCategory = await _context.PortfolioCategories
            .Where(x => x.Id != id)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync();

        var albumsInCategory = await _context.PortfolioAlbums
            .Where(x => x.PortfolioCategoryId == id)
            .ToListAsync();

        if (albumsInCategory.Count > 0 && fallbackCategory == null)
            return BadRequest(new { message = "Не можеш да изтриеш последната категория, докато има албуми в нея." });

        foreach (var album in albumsInCategory)
        {
            album.PortfolioCategoryId = fallbackCategory!.Id;
        }

        _context.PortfolioCategories.Remove(entity);
        await _context.SaveChangesAsync();

        var categories = await _context.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < categories.Count; i++)
        {
            categories[i].DisplayOrder = i + 1;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums()
    {
        var items = await _context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .OrderBy(x => x.PortfolioCategoryId)
            .ThenBy(x => x.DisplayOrder)
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

    [HttpPost("albums")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreatePortfolioAlbumRequest model)
    {
        if (!await _context.PortfolioCategories.AnyAsync(x => x.Id == model.PortfolioCategoryId))
            return BadRequest(new { message = "Невалидна категория." });

        var slug = (model.Slug ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { message = "Slug е задължителен." });

        var titleBg = (model.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titleBg))
            return BadRequest(new { message = "Заглавието на български е задължително." });

        var duplicateSlugExists = await _context.PortfolioAlbums.AnyAsync(x => x.Slug == slug);
        if (duplicateSlugExists)
            return BadRequest(new { message = "Вече съществува албум със същия slug." });

        var entity = new PortfolioAlbum
        {
            PortfolioCategoryId = model.PortfolioCategoryId,
            Slug = slug,
            Title = titleBg,
            TitleEn = string.IsNullOrWhiteSpace(model.TitleEn) ? null : model.TitleEn.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(model.CoverImageUrl) ? null : model.CoverImageUrl.Trim(),
            DisplayOrder = model.DisplayOrder,
            ColumnNumber = model.ColumnNumber,
            IsPublished = model.IsPublished,
            IsActive = model.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.PortfolioAlbums.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            entity.Id,
            entity.PortfolioCategoryId,
            entity.Slug,
            entity.Title,
            entity.TitleEn,
            entity.Description,
            entity.CoverImageUrl,
            entity.DisplayOrder,
            entity.ColumnNumber,
            entity.IsPublished,
            entity.IsActive,
            entity.AllowClientAccess,
            entity.CreatedAtUtc
        });
    }

    [HttpPut("albums/{id:int}")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] UpdatePortfolioAlbumRequest model)
    {
        var entity = await _context.PortfolioAlbums.FindAsync(id);
        if (entity == null)
            return NotFound();

        if (!await _context.PortfolioCategories.AnyAsync(x => x.Id == model.PortfolioCategoryId))
            return BadRequest(new { message = "Невалидна категория." });

        var slug = (model.Slug ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { message = "Slug е задължителен." });

        var titleBg = (model.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titleBg))
            return BadRequest(new { message = "Заглавието на български е задължително." });

        var duplicateSlugExists = await _context.PortfolioAlbums.AnyAsync(x => x.Id != id && x.Slug == slug);
        if (duplicateSlugExists)
            return BadRequest(new { message = "Вече съществува албум със същия slug." });

        entity.PortfolioCategoryId = model.PortfolioCategoryId;
        entity.Slug = slug;
        entity.Title = titleBg;
        entity.TitleEn = string.IsNullOrWhiteSpace(model.TitleEn) ? null : model.TitleEn.Trim();
        entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        entity.CoverImageUrl = string.IsNullOrWhiteSpace(model.CoverImageUrl) ? null : model.CoverImageUrl.Trim();
        entity.DisplayOrder = model.DisplayOrder;
        entity.ColumnNumber = model.ColumnNumber;
        entity.IsPublished = model.IsPublished;
        entity.IsActive = model.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            entity.Id,
            entity.PortfolioCategoryId,
            entity.Slug,
            entity.Title,
            entity.TitleEn,
            entity.Description,
            entity.CoverImageUrl,
            entity.DisplayOrder,
            entity.ColumnNumber,
            entity.IsPublished,
            entity.IsActive,
            entity.AllowClientAccess,
            entity.CreatedAtUtc
        });
    }

    [HttpDelete("albums/{id:int}")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var entity = await _context.PortfolioAlbums.FindAsync(id);
        if (entity == null)
            return NotFound();

        _context.PortfolioAlbums.Remove(entity);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("images")]
    public async Task<IActionResult> GetImages()
    {
        var items = await _context.PortfolioImages
            .Include(x => x.PortfolioAlbum)
            .OrderBy(x => x.PortfolioAlbumId)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("images")]
    public async Task<IActionResult> CreateImage([FromBody] PortfolioImage entity)
    {
        entity.CreatedAtUtc = DateTime.UtcNow;
        _context.PortfolioImages.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(entity);
    }

    [HttpPut("images/{id:int}")]
    public async Task<IActionResult> UpdateImage(int id, [FromBody] PortfolioImage model)
    {
        var entity = await _context.PortfolioImages.FindAsync(id);
        if (entity == null)
            return NotFound();

        entity.PortfolioAlbumId = model.PortfolioAlbumId;
        entity.ImageUrl = model.ImageUrl;
        entity.ThumbnailUrl = model.ThumbnailUrl;
        entity.AltText = model.AltText;
        entity.Caption = model.Caption;
        entity.Width = model.Width;
        entity.Height = model.Height;
        entity.DisplayOrder = model.DisplayOrder;
        entity.IsCover = model.IsCover;
        entity.IsPublished = model.IsPublished;

        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("images/{id:int}")]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var entity = await _context.PortfolioImages.FindAsync(id);
        if (entity == null)
            return NotFound();

        _context.PortfolioImages.Remove(entity);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}