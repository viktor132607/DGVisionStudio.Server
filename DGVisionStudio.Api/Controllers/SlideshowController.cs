using System.Text.Json;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

public class SlideshowImageDto
{
	public int Id { get; set; }
	public string ImageUrl { get; set; } = string.Empty;
	public string? ThumbnailUrl { get; set; }
	public string? AltText { get; set; }
	public string? Caption { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsPublished { get; set; }
	public int PortfolioAlbumId { get; set; }
	public string? AlbumTitle { get; set; }
	public string? CategoryName { get; set; }
	public string? CategoryNameEn { get; set; }
	public bool IsSelected { get; set; }
	public int? SlideshowOrder { get; set; }
}

public class AdminSlideshowResponse
{
	public List<SlideshowImageDto> SelectedImages { get; set; } = new();
	public List<SlideshowImageDto> AvailableImages { get; set; } = new();
	public List<int> ImageIds { get; set; } = new();
	public string? IntroVideoUrl { get; set; }
}

public class SlideshowIntroVideoResponse
{
	public string? VideoUrl { get; set; }
}

public class UpdateHomeSlideshowRequest
{
	public List<int>? ImageIds { get; set; }
}

internal class HomeSlideshowSettings
{
	public List<int> ImageIds { get; set; } = new();
}

internal static class HomeSlideshowSettingsHelper
{
	public const string SettingKey = "home-slideshow-image-ids";
	public const string IntroVideoSettingKey = "home-slideshow-intro-video-url";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static IQueryable<PortfolioImage> GetAvailableImagesQuery(AppDbContext context) =>
		context.PortfolioImages
			.Include(x => x.PortfolioAlbum)
			.ThenInclude(x => x.PortfolioCategory)
			.Where(x =>
				x.IsPublished &&
				x.PortfolioAlbum != null &&
				x.PortfolioAlbum.IsPublished &&
				!x.PortfolioAlbum.IsUserUploaded &&
				x.PortfolioAlbum.PortfolioCategory != null &&
				x.PortfolioAlbum.PortfolioCategory.IsActive);

	public static IQueryable<PortfolioImage> ApplyDefaultOrder(IQueryable<PortfolioImage> query) =>
		query
			.OrderBy(x => x.PortfolioAlbum!.PortfolioCategory!.DisplayOrder)
			.ThenBy(x => x.PortfolioAlbum!.DisplayOrder)
			.ThenBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id);

	public static SlideshowImageDto ToDto(PortfolioImage image, bool isSelected = false, int? slideshowOrder = null) =>
		new()
		{
			Id = image.Id,
			ImageUrl = image.ImageUrl,
			ThumbnailUrl = image.ThumbnailUrl,
			AltText = image.AltText,
			Caption = image.Caption,
			DisplayOrder = image.DisplayOrder,
			IsPublished = image.IsPublished,
			PortfolioAlbumId = image.PortfolioAlbumId,
			AlbumTitle = image.PortfolioAlbum?.Title,
			CategoryName = image.PortfolioAlbum?.PortfolioCategory?.Name,
			CategoryNameEn = image.PortfolioAlbum?.PortfolioCategory?.NameEn,
			IsSelected = isSelected,
			SlideshowOrder = slideshowOrder
		};

	public static async Task<List<int>> GetSelectedImageIdsAsync(AppDbContext context)
	{
		var setting = await context.SiteSettings
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Key == SettingKey);

		if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
			return new List<int>();

		try
		{
			var parsed = JsonSerializer.Deserialize<HomeSlideshowSettings>(setting.Value, JsonOptions);
			return NormalizeIds(parsed?.ImageIds);
		}
		catch
		{
			try
			{
				var parsed = JsonSerializer.Deserialize<List<int>>(setting.Value, JsonOptions);
				return NormalizeIds(parsed);
			}
			catch
			{
				return new List<int>();
			}
		}
	}

	public static async Task SaveSelectedImageIdsAsync(AppDbContext context, List<int> imageIds)
	{
		var normalizedIds = NormalizeIds(imageIds);
		var value = JsonSerializer.Serialize(new HomeSlideshowSettings { ImageIds = normalizedIds }, JsonOptions);
		var setting = await context.SiteSettings.FirstOrDefaultAsync(x => x.Key == SettingKey);

		if (setting == null)
		{
			context.SiteSettings.Add(new SiteSetting
			{
				Key = SettingKey,
				Value = value,
				Description = "Selected image ids and order for the home page slideshow.",
				UpdatedAtUtc = DateTime.UtcNow
			});
		}
		else
		{
			setting.Value = value;
			setting.Description = "Selected image ids and order for the home page slideshow.";
			setting.UpdatedAtUtc = DateTime.UtcNow;
		}
	}

	public static async Task<string?> GetIntroVideoUrlAsync(AppDbContext context)
	{
		var setting = await context.SiteSettings
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Key == IntroVideoSettingKey);

		return string.IsNullOrWhiteSpace(setting?.Value) ? null : setting.Value.Trim();
	}

	public static async Task SaveIntroVideoUrlAsync(AppDbContext context, string? videoUrl)
	{
		var setting = await context.SiteSettings.FirstOrDefaultAsync(x => x.Key == IntroVideoSettingKey);
		var normalizedUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();

		if (normalizedUrl == null)
		{
			if (setting != null) context.SiteSettings.Remove(setting);
			return;
		}

		if (setting == null)
		{
			context.SiteSettings.Add(new SiteSetting
			{
				Key = IntroVideoSettingKey,
				Value = normalizedUrl,
				Description = "Optional intro video shown once before the home page slideshow.",
				UpdatedAtUtc = DateTime.UtcNow
			});
		}
		else
		{
			setting.Value = normalizedUrl;
			setting.Description = "Optional intro video shown once before the home page slideshow.";
			setting.UpdatedAtUtc = DateTime.UtcNow;
		}
	}

	public static List<int> NormalizeIds(IEnumerable<int>? ids)
	{
		var result = new List<int>();
		var seen = new HashSet<int>();

		foreach (var id in ids ?? Enumerable.Empty<int>())
		{
			if (id < 1 || !seen.Add(id))
				continue;

			result.Add(id);
		}

		return result;
	}
}

[ApiController]
[Route("api/portfolio/slideshow")]
public class PortfolioSlideshowController : ControllerBase
{
	private readonly AppDbContext _context;

	public PortfolioSlideshowController(AppDbContext context)
	{
		_context = context;
	}

	[HttpGet]
	public async Task<IActionResult> GetSlideshowImages()
	{
		var selectedIds = await HomeSlideshowSettingsHelper.GetSelectedImageIdsAsync(_context);
		var query = HomeSlideshowSettingsHelper.GetAvailableImagesQuery(_context).AsNoTracking();

		if (selectedIds.Count == 0)
		{
			var defaultItems = await HomeSlideshowSettingsHelper
				.ApplyDefaultOrder(query)
				.ToListAsync();

			return Ok(defaultItems.Select(x => HomeSlideshowSettingsHelper.ToDto(x)).ToList());
		}

		var selectedSet = selectedIds.ToHashSet();
		var selectedImages = await query
			.Where(x => selectedSet.Contains(x.Id))
			.ToListAsync();

		var selectedById = selectedImages.ToDictionary(x => x.Id);
		var orderedItems = selectedIds
			.Select((id, index) => selectedById.TryGetValue(id, out var image)
				? HomeSlideshowSettingsHelper.ToDto(image, true, index + 1)
				: null)
			.Where(x => x != null)
			.ToList();

		return Ok(orderedItems);
	}

	[HttpGet("intro-video")]
	public async Task<IActionResult> GetIntroVideo()
	{
		return Ok(new SlideshowIntroVideoResponse
		{
			VideoUrl = await HomeSlideshowSettingsHelper.GetIntroVideoUrlAsync(_context)
		});
	}
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/slideshow")]
public class AdminSlideshowController : ControllerBase
{
	private readonly AppDbContext _context;
	private readonly IWebHostEnvironment _environment;

	public AdminSlideshowController(AppDbContext context, IWebHostEnvironment environment)
	{
		_context = context;
		_environment = environment;
	}

	[HttpGet]
	public async Task<IActionResult> GetSlideshowManagement()
	{
		var availableImages = await HomeSlideshowSettingsHelper
			.ApplyDefaultOrder(HomeSlideshowSettingsHelper.GetAvailableImagesQuery(_context).AsNoTracking())
			.ToListAsync();

		var savedIds = await HomeSlideshowSettingsHelper.GetSelectedImageIdsAsync(_context);
		var currentIds = savedIds.Count > 0
			? savedIds
			: availableImages.Select(x => x.Id).ToList();

		var availableById = availableImages.ToDictionary(x => x.Id);
		var currentIdSet = currentIds.ToHashSet();

		var selectedImages = currentIds
			.Select((id, index) => availableById.TryGetValue(id, out var image)
				? HomeSlideshowSettingsHelper.ToDto(image, true, index + 1)
				: null)
			.Where(x => x != null)
			.ToList();

		var allImages = availableImages
			.Select(image =>
			{
				var selectedIndex = currentIds.IndexOf(image.Id);
				return HomeSlideshowSettingsHelper.ToDto(
					image,
					currentIdSet.Contains(image.Id),
					selectedIndex >= 0 ? selectedIndex + 1 : null);
			})
			.ToList();

		return Ok(new AdminSlideshowResponse
		{
			SelectedImages = selectedImages!,
			AvailableImages = allImages,
			ImageIds = selectedImages!.Select(x => x.Id).ToList(),
			IntroVideoUrl = await HomeSlideshowSettingsHelper.GetIntroVideoUrlAsync(_context)
		});
	}

	[HttpPut]
	public async Task<IActionResult> UpdateSlideshow([FromBody] UpdateHomeSlideshowRequest model)
	{
		var requestedIds = HomeSlideshowSettingsHelper.NormalizeIds(model.ImageIds);
		var availableIds = await HomeSlideshowSettingsHelper
			.GetAvailableImagesQuery(_context)
			.AsNoTracking()
			.Where(x => requestedIds.Contains(x.Id))
			.Select(x => x.Id)
			.ToListAsync();

		var availableSet = availableIds.ToHashSet();
		var validIds = requestedIds.Where(availableSet.Contains).ToList();

		await HomeSlideshowSettingsHelper.SaveSelectedImageIdsAsync(_context, validIds);
		await _context.SaveChangesAsync();

		return NoContent();
	}

	[HttpPost("video")]
	[RequestSizeLimit(200_000_000)]
	public async Task<IActionResult> UploadIntroVideo([FromForm] IFormFile file)
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "Избери видео файл." });

		if (!IsAllowedVideo(file))
			return BadRequest(new { message = "Позволени са само видео файлове: mp4, mov, webm, m4v." });

		var oldVideoUrl = await HomeSlideshowSettingsHelper.GetIntroVideoUrlAsync(_context);
		var uploadsFolder = GetSlideshowUploadFolder();
		Directory.CreateDirectory(uploadsFolder);

		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var fileName = $"intro-{Guid.NewGuid():N}{extension}";
		var filePath = Path.Combine(uploadsFolder, fileName);

		await using (var stream = System.IO.File.Create(filePath))
		{
			await file.CopyToAsync(stream);
		}

		var videoUrl = $"/uploads/slideshow/{fileName}";
		await HomeSlideshowSettingsHelper.SaveIntroVideoUrlAsync(_context, videoUrl);
		await _context.SaveChangesAsync();
		DeleteLocalUpload(oldVideoUrl);

		return Ok(new SlideshowIntroVideoResponse { VideoUrl = videoUrl });
	}

	[HttpDelete("video")]
	public async Task<IActionResult> DeleteIntroVideo()
	{
		var oldVideoUrl = await HomeSlideshowSettingsHelper.GetIntroVideoUrlAsync(_context);
		await HomeSlideshowSettingsHelper.SaveIntroVideoUrlAsync(_context, null);
		await _context.SaveChangesAsync();
		DeleteLocalUpload(oldVideoUrl);

		return NoContent();
	}

	private static bool IsAllowedVideo(IFormFile file)
	{
		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".mp4",
			".mov",
			".webm",
			".m4v"
		};

		return allowedExtensions.Contains(extension) || file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
	}

	private string GetSlideshowUploadFolder()
	{
		var webRootPath = _environment.WebRootPath;
		if (string.IsNullOrWhiteSpace(webRootPath))
		{
			webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
		}

		return Path.Combine(webRootPath, "uploads", "slideshow");
	}

	private void DeleteLocalUpload(string? url)
	{
		if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/slideshow/", StringComparison.OrdinalIgnoreCase))
			return;

		var fileName = Path.GetFileName(url);
		if (string.IsNullOrWhiteSpace(fileName)) return;

		var path = Path.Combine(GetSlideshowUploadFolder(), fileName);
		if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
	}
}
