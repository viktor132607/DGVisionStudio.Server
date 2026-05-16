using System.Security.Claims;
using DGVisionStudio.Infrastructure.DTOs;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/portfolio")]
public class AdminPortfolioController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly IAuditLogService _auditLogService;
	private readonly ILogger<AdminPortfolioController> _logger;

	public AdminPortfolioController(
		AppDbContext context,
		IAuditLogService auditLogService,
		ILogger<AdminPortfolioController> logger)
	{
		_context = context;
		_auditLogService = auditLogService;
		_logger = logger;
	}

	[HttpGet("categories")]
	public async Task<IActionResult> GetCategories() =>
		Ok(await _context.PortfolioCategories
			.AsNoTracking()
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id)
			.ToListAsync());

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
		await using var transaction = await _context.Database.BeginTransactionAsync();

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
			.AsNoTracking()
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
			IsActive = model.IsActive,
			IsDeleted = false,
			DeletedAtUtc = null
		};

		_context.PortfolioCategories.Add(entity);
		await _context.SaveChangesAsync();

		await AuditAsync("CreatePortfolioCategory", "PortfolioCategory", entity.Id.ToString(), null, entity);

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin created portfolio category. CategoryId: {CategoryId}, Key: {Key}, Name: {Name}, IsActive: {IsActive}, Admin: {Admin}, TraceId: {TraceId}",
			entity.Id,
			entity.Key,
			entity.Name,
			entity.IsActive,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return Ok(entity);
	}

	[HttpPut("categories/{id:int}")]
	public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdatePortfolioCategoryRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioCategories.FindAsync(id);
		if (entity == null)
			return NotFound();

		var oldValue = new
		{
			entity.Id,
			entity.Key,
			entity.Name,
			entity.NameEn,
			entity.Description,
			entity.DisplayOrder,
			entity.IsActive,
			entity.IsDeleted,
			entity.DeletedAtUtc
		};

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
			.AsNoTracking()
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

		await AuditAsync(
			"UpdatePortfolioCategory",
			"PortfolioCategory",
			entity.Id.ToString(),
			oldValue,
			new
			{
				entity.Id,
				entity.Key,
				entity.Name,
				entity.NameEn,
				entity.Description,
				entity.DisplayOrder,
				entity.IsActive,
				entity.IsDeleted,
				entity.DeletedAtUtc
			});

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin updated portfolio category. CategoryId: {CategoryId}, Key: {Key}, Name: {Name}, IsActive: {IsActive}, Admin: {Admin}, TraceId: {TraceId}",
			entity.Id,
			entity.Key,
			entity.Name,
			entity.IsActive,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return Ok(entity);
	}

	[HttpPut("categories/{id:int}/move")]
	public async Task<IActionResult> MoveCategory(int id, [FromBody] MovePortfolioCategoryRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioCategories.FindAsync(id);
		if (entity == null)
			return NotFound();

		var oldValue = new
		{
			entity.Id,
			entity.DisplayOrder
		};

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

		await AuditAsync(
			"MovePortfolioCategory",
			"PortfolioCategory",
			id.ToString(),
			oldValue,
			new
			{
				CategoryId = id,
				RequestedDisplayOrder = model.DisplayOrder,
				NewDisplayOrder = source.DisplayOrder
			});

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin moved portfolio category. CategoryId: {CategoryId}, RequestedDisplayOrder: {RequestedDisplayOrder}, Admin: {Admin}, TraceId: {TraceId}",
			id,
			model.DisplayOrder,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

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
			.Where(x => !x.IsUserUploaded)
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id)
			.Select(x => new
			{
				x.Id,
				x.Title,
				x.Slug,
				x.CoverImageUrl,
				x.DisplayOrder,
				x.IsPublished,
				x.PortfolioCategoryId,
				IsSelected = x.PortfolioCategoryId == id
			})
			.ToListAsync();

		return Ok(new
		{
			category,
			albums
		});
	}

	[HttpPut("categories/{id:int}/albums")]
	public async Task<IActionResult> UpdateCategoryAlbums(int id, [FromBody] UpdateCategoryAlbumsRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var categoryExists = await _context.PortfolioCategories
			.AsNoTracking()
			.AnyAsync(x => x.Id == id);

		if (!categoryExists)
			return NotFound();

		var selectedAlbumIds = (model.AlbumIds ?? new List<int>())
			.Distinct()
			.ToHashSet();

		var albums = await _context.PortfolioAlbums
			.Where(x => !x.IsUserUploaded)
			.ToListAsync();

		var oldValue = albums
			.Where(x => x.PortfolioCategoryId == id)
			.Select(x => new { x.Id, x.Title, x.PortfolioCategoryId })
			.ToList();

		foreach (var album in albums)
		{
			if (selectedAlbumIds.Contains(album.Id))
			{
				album.PortfolioCategoryId = id;
			}
		}

		await _context.SaveChangesAsync();

		await AuditAsync(
			"UpdatePortfolioCategoryAlbums",
			"PortfolioCategory",
			id.ToString(),
			oldValue,
			new
			{
				CategoryId = id,
				SelectedAlbumIds = selectedAlbumIds
			});

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin updated category albums. CategoryId: {CategoryId}, SelectedAlbumCount: {SelectedAlbumCount}, Admin: {Admin}, TraceId: {TraceId}",
			id,
			selectedAlbumIds.Count,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return NoContent();
	}

	[HttpDelete("categories/{id:int}")]
	public async Task<IActionResult> DeleteCategory(int id)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioCategories
			.Include(x => x.Albums)
			.FirstOrDefaultAsync(x => x.Id == id);

		if (entity == null)
			return NotFound();

		var now = DateTime.UtcNow;

		var oldValue = new
		{
			entity.Id,
			entity.Key,
			entity.Name,
			entity.NameEn,
			entity.Description,
			entity.DisplayOrder,
			entity.IsActive,
			entity.IsDeleted,
			entity.DeletedAtUtc
		};

		var albumsInCategory = await _context.PortfolioAlbums
			.Where(x => x.PortfolioCategoryId == id && !x.IsUserUploaded)
			.Include(x => x.Images)
			.ToListAsync();

		entity.IsDeleted = true;
		entity.DeletedAtUtc = now;
		entity.IsActive = false;

		foreach (var album in albumsInCategory)
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

		await AuditAsync(
			"SoftDeletePortfolioCategory",
			"PortfolioCategory",
			id.ToString(),
			oldValue,
			new
			{
				DeletedCategoryId = id,
				SoftDeletedAlbumCount = albumsInCategory.Count,
				SoftDeletedImageCount = albumsInCategory.Sum(x => x.Images.Count),
				IsDeleted = true,
				DeletedAtUtc = now
			});

		await transaction.CommitAsync();

		_logger.LogWarning(
			"Admin soft deleted portfolio category. CategoryId: {CategoryId}, Key: {Key}, SoftDeletedAlbumCount: {SoftDeletedAlbumCount}, Admin: {Admin}, TraceId: {TraceId}",
			id,
			oldValue.Key,
			albumsInCategory.Count,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return NoContent();
	}

	[HttpGet("albums")]
	public async Task<IActionResult> GetAlbums([FromQuery] PagedQueryDto query)
	{
		var page = query.Page;
		var pageSize = query.PageSize;

		var source = _context.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.PortfolioCategory)
			.Where(x => !x.IsUserUploaded)
			.OrderBy(x => x.PortfolioCategoryId)
			.ThenBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id);

		var total = await source.CountAsync();

		var items = await source
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		return Ok(new PagedResultDto<PortfolioAlbum>
		{
			Page = page,
			PageSize = pageSize,
			Total = total,
			Items = items
		});
	}

	[HttpPost("albums")]
	public async Task<IActionResult> CreateAlbum([FromBody] CreatePortfolioAlbumRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		if (!await _context.PortfolioCategories.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioCategoryId))
			return BadRequest(new { message = "Невалидна категория." });

		var slug = (model.Slug ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(slug))
			return BadRequest(new { message = "Slug е задължителен." });

		var titleBg = (model.Title ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(titleBg))
			return BadRequest(new { message = "Заглавието на български е задължително." });

		var duplicateSlugExists = await _context.PortfolioAlbums
			.AsNoTracking()
			.AnyAsync(x => x.Slug == slug);

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
			IsUserUploaded = false,
			IsSeenByAdmin = true,
			IsDeleted = false,
			DeletedAtUtc = null,
			CreatedAtUtc = DateTime.UtcNow
		};

		_context.PortfolioAlbums.Add(entity);
		await _context.SaveChangesAsync();

		await AuditAsync("CreatePortfolioAlbum", "PortfolioAlbum", entity.Id.ToString(), null, entity);

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin created portfolio album. AlbumId: {AlbumId}, Slug: {Slug}, Title: {Title}, CategoryId: {CategoryId}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			entity.Id,
			entity.Slug,
			entity.Title,
			entity.PortfolioCategoryId,
			entity.IsPublished,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return Ok(entity);
	}

	[HttpPut("albums/{id:int}")]
	public async Task<IActionResult> UpdateAlbum(int id, [FromBody] UpdatePortfolioAlbumRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioAlbums
			.FirstOrDefaultAsync(x => x.Id == id && !x.IsUserUploaded);

		if (entity == null)
			return NotFound();

		var oldValue = new
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
			entity.IsDeleted,
			entity.DeletedAtUtc
		};

		if (!await _context.PortfolioCategories.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioCategoryId))
			return BadRequest(new { message = "Невалидна категория." });

		var slug = (model.Slug ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(slug))
			return BadRequest(new { message = "Slug е задължителен." });

		var titleBg = (model.Title ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(titleBg))
			return BadRequest(new { message = "Заглавието на български е задължително." });

		var duplicateSlugExists = await _context.PortfolioAlbums
			.AsNoTracking()
			.AnyAsync(x => x.Id != id && x.Slug == slug);

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

		await _context.SaveChangesAsync();

		await AuditAsync(
			"UpdatePortfolioAlbum",
			"PortfolioAlbum",
			entity.Id.ToString(),
			oldValue,
			new
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
				entity.IsDeleted,
				entity.DeletedAtUtc
			});

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin updated portfolio album. AlbumId: {AlbumId}, Slug: {Slug}, Title: {Title}, CategoryId: {CategoryId}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			entity.Id,
			entity.Slug,
			entity.Title,
			entity.PortfolioCategoryId,
			entity.IsPublished,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return Ok(entity);
	}

	[HttpDelete("albums/{id:int}")]
	public async Task<IActionResult> DeleteAlbum(int id)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioAlbums
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x => x.Id == id && !x.IsUserUploaded);

		if (entity == null)
			return NotFound();

		var now = DateTime.UtcNow;

		var oldValue = new
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
			entity.IsDeleted,
			entity.DeletedAtUtc
		};

		entity.IsDeleted = true;
		entity.DeletedAtUtc = now;
		entity.IsPublished = false;
		entity.AllowClientAccess = false;

		foreach (var image in entity.Images)
		{
			image.IsDeleted = true;
			image.DeletedAtUtc = now;
			image.IsPublished = false;
			image.IsCover = false;
		}

		await _context.SaveChangesAsync();

		await AuditAsync(
			"SoftDeletePortfolioAlbum",
			"PortfolioAlbum",
			id.ToString(),
			oldValue,
			new
			{
				DeletedAlbumId = id,
				SoftDeletedImageCount = entity.Images.Count,
				IsDeleted = true,
				DeletedAtUtc = now
			});

		await transaction.CommitAsync();

		_logger.LogWarning(
			"Admin soft deleted portfolio album. AlbumId: {AlbumId}, Slug: {Slug}, Title: {Title}, SoftDeletedImageCount: {SoftDeletedImageCount}, Admin: {Admin}, TraceId: {TraceId}",
			id,
			oldValue.Slug,
			oldValue.Title,
			entity.Images.Count,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return NoContent();
	}

	[HttpGet("images")]
	public async Task<IActionResult> GetImages([FromQuery] PagedQueryDto query)
	{
		var page = query.Page;
		var pageSize = query.PageSize;

		var source = _context.PortfolioImages
			.AsNoTracking()
			.Include(x => x.PortfolioAlbum)
			.Where(x => x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded)
			.OrderBy(x => x.PortfolioAlbumId)
			.ThenBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id);

		var total = await source.CountAsync();

		var items = await source
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		return Ok(new PagedResultDto<PortfolioImage>
		{
			Page = page,
			PageSize = pageSize,
			Total = total,
			Items = items
		});
	}

	[HttpPost("images")]
	public async Task<IActionResult> CreateImage([FromBody] CreatePortfolioImageRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		if (!await _context.PortfolioAlbums.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioAlbumId && !x.IsUserUploaded))
			return BadRequest(new { message = "Невалиден албум." });

		var imageUrl = (model.ImageUrl ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(imageUrl))
			return BadRequest(new { message = "ImageUrl е задължителен." });

		var entity = new PortfolioImage
		{
			PortfolioAlbumId = model.PortfolioAlbumId,
			ImageUrl = imageUrl,
			ThumbnailUrl = string.IsNullOrWhiteSpace(model.ThumbnailUrl) ? null : model.ThumbnailUrl.Trim(),
			AltText = string.IsNullOrWhiteSpace(model.AltText) ? null : model.AltText.Trim(),
			Caption = string.IsNullOrWhiteSpace(model.Caption) ? null : model.Caption.Trim(),
			Width = model.Width ?? 0,
			Height = model.Height ?? 0,
			DisplayOrder = model.DisplayOrder,
			IsCover = model.IsCover,
			IsPublished = model.IsPublished,
			IsDeleted = false,
			DeletedAtUtc = null,
			CreatedAtUtc = DateTime.UtcNow
		};

		_context.PortfolioImages.Add(entity);
		await _context.SaveChangesAsync();

		await AuditAsync("CreatePortfolioImage", "PortfolioImage", entity.Id.ToString(), null, entity);

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin created portfolio image. ImageId: {ImageId}, AlbumId: {AlbumId}, ImageUrl: {ImageUrl}, IsCover: {IsCover}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			entity.Id,
			entity.PortfolioAlbumId,
			entity.ImageUrl,
			entity.IsCover,
			entity.IsPublished,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return Ok(entity);
	}

	[HttpPut("images/{id:int}")]
	public async Task<IActionResult> UpdateImage(int id, [FromBody] UpdatePortfolioImageRequest model)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioImages
			.Include(x => x.PortfolioAlbum)
			.FirstOrDefaultAsync(x => x.Id == id && x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded);

		if (entity == null)
			return NotFound();

		var oldValue = new
		{
			entity.Id,
			entity.PortfolioAlbumId,
			entity.ImageUrl,
			entity.ThumbnailUrl,
			entity.AltText,
			entity.Caption,
			entity.Width,
			entity.Height,
			entity.DisplayOrder,
			entity.IsCover,
			entity.IsPublished,
			entity.IsDeleted,
			entity.DeletedAtUtc
		};

		if (!await _context.PortfolioAlbums.AsNoTracking().AnyAsync(x => x.Id == model.PortfolioAlbumId && !x.IsUserUploaded))
			return BadRequest(new { message = "Невалиден албум." });

		var imageUrl = (model.ImageUrl ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(imageUrl))
			return BadRequest(new { message = "ImageUrl е задължителен." });

		entity.PortfolioAlbumId = model.PortfolioAlbumId;
		entity.ImageUrl = imageUrl;
		entity.ThumbnailUrl = string.IsNullOrWhiteSpace(model.ThumbnailUrl) ? null : model.ThumbnailUrl.Trim();
		entity.AltText = string.IsNullOrWhiteSpace(model.AltText) ? null : model.AltText.Trim();
		entity.Caption = string.IsNullOrWhiteSpace(model.Caption) ? null : model.Caption.Trim();
		entity.Width = model.Width ?? 0;
		entity.Height = model.Height ?? 0;
		entity.DisplayOrder = model.DisplayOrder;
		entity.IsCover = model.IsCover;
		entity.IsPublished = model.IsPublished;

		await _context.SaveChangesAsync();

		await AuditAsync(
			"UpdatePortfolioImage",
			"PortfolioImage",
			entity.Id.ToString(),
			oldValue,
			new
			{
				entity.Id,
				entity.PortfolioAlbumId,
				entity.ImageUrl,
				entity.ThumbnailUrl,
				entity.AltText,
				entity.Caption,
				entity.Width,
				entity.Height,
				entity.DisplayOrder,
				entity.IsCover,
				entity.IsPublished,
				entity.IsDeleted,
				entity.DeletedAtUtc
			});

		await transaction.CommitAsync();

		_logger.LogInformation(
			"Admin updated portfolio image. ImageId: {ImageId}, AlbumId: {AlbumId}, ImageUrl: {ImageUrl}, IsCover: {IsCover}, IsPublished: {IsPublished}, Admin: {Admin}, TraceId: {TraceId}",
			entity.Id,
			entity.PortfolioAlbumId,
			entity.ImageUrl,
			entity.IsCover,
			entity.IsPublished,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return Ok(entity);
	}

	[HttpDelete("images/{id:int}")]
	public async Task<IActionResult> DeleteImage(int id)
	{
		await using var transaction = await _context.Database.BeginTransactionAsync();

		var entity = await _context.PortfolioImages
			.Include(x => x.PortfolioAlbum)
			.FirstOrDefaultAsync(x => x.Id == id && x.PortfolioAlbum != null && !x.PortfolioAlbum.IsUserUploaded);

		if (entity == null)
			return NotFound();

		var now = DateTime.UtcNow;

		var oldValue = new
		{
			entity.Id,
			entity.PortfolioAlbumId,
			entity.ImageUrl,
			entity.ThumbnailUrl,
			entity.AltText,
			entity.Caption,
			entity.Width,
			entity.Height,
			entity.DisplayOrder,
			entity.IsCover,
			entity.IsPublished,
			entity.IsDeleted,
			entity.DeletedAtUtc
		};

		entity.IsDeleted = true;
		entity.DeletedAtUtc = now;
		entity.IsPublished = false;
		entity.IsCover = false;

		await _context.SaveChangesAsync();

		await AuditAsync(
			"SoftDeletePortfolioImage",
			"PortfolioImage",
			id.ToString(),
			oldValue,
			new
			{
				DeletedImageId = id,
				IsDeleted = true,
				DeletedAtUtc = now
			});

		await transaction.CommitAsync();

		_logger.LogWarning(
			"Admin soft deleted portfolio image. ImageId: {ImageId}, AlbumId: {AlbumId}, ImageUrl: {ImageUrl}, Admin: {Admin}, TraceId: {TraceId}",
			id,
			oldValue.PortfolioAlbumId,
			oldValue.ImageUrl,
			User.Identity?.Name,
			HttpContext.TraceIdentifier);

		return NoContent();
	}

	private async Task AuditAsync(
		string action,
		string entityType,
		string? entityId,
		object? oldValue,
		object? newValue)
	{
		await _auditLogService.LogAsync(
			GetAdminUserId(),
			GetAdminEmail(),
			action,
			entityType,
			entityId,
			oldValue,
			newValue,
			HttpContext.Connection.RemoteIpAddress?.ToString(),
			Request.Headers.UserAgent.ToString(),
			HttpContext.TraceIdentifier);
	}

	private string GetAdminUserId()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
	}

	private string GetAdminEmail()
	{
		return User.FindFirstValue(ClaimTypes.Email)
			?? User.Identity?.Name
			?? string.Empty;
	}
}