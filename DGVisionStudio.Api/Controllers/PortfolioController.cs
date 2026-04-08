using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/portfolio")]
public class PortfolioController : ControllerBase
{
	private readonly AppDbContext _context;

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
			.Include(x => x.PortfolioCategory)
			.Where(x =>
				x.IsPublished &&
				x.PortfolioCategory != null &&
				x.PortfolioCategory.IsActive)
			.AsQueryable();

		if (categoryId.HasValue)
			query = query.Where(x => x.PortfolioCategoryId == categoryId.Value);

		var items = await query
			.OrderBy(x => x.DisplayOrder)
			.ThenByDescending(x => x.CreatedAtUtc)
			.ThenBy(x => x.Id)
			.ToListAsync();

		return Ok(items);
	}

	[HttpGet("albums/{slug}")]
	public async Task<IActionResult> GetAlbum(string slug)
	{
		var album = await _context.PortfolioAlbums
			.Include(x => x.PortfolioCategory)
			.Include(x => x.Images.Where(i => i.IsPublished).OrderBy(i => i.DisplayOrder))
			.FirstOrDefaultAsync(x =>
				x.Slug == slug &&
				x.IsPublished &&
				x.PortfolioCategory != null &&
				x.PortfolioCategory.IsActive);

		return album is null ? NotFound() : Ok(album);
	}

	[HttpGet("images")]
	public async Task<IActionResult> GetImages([FromQuery] int? albumId = null)
	{
		var query = _context.PortfolioImages
			.Include(x => x.PortfolioAlbum)
			.ThenInclude(x => x.PortfolioCategory)
			.Where(x =>
				x.IsPublished &&
				x.PortfolioAlbum != null &&
				x.PortfolioAlbum.IsPublished &&
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