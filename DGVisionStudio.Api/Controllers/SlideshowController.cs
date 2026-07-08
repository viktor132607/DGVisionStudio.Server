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
	public bool UseDefaultInterval { get; set; }
	public int IntervalMs { get; set; }
	public int DefaultIntervalMs { get; set; }
	public int MinIntervalMs { get; set; }
	public int MaxIntervalMs { get; set; }
}

public class SlideshowIntroVideoResponse
{
	public string? VideoUrl { get; set; }
}

public class SlideshowSettingsResponse
{
	public bool UseDefaultInterval { get; set; }
	public int IntervalMs { get; set; }
	public int DefaultIntervalMs { get; set; }
	public int MinIntervalMs { get; set; }
	public int MaxIntervalMs { get; set; }
}

public class UpdateHomeSlideshowRequest
{
	public List<int>? ImageIds { get; set; }
	public bool? UseDefaultInterval { get; set; }
	public int? IntervalMs { get; set; }
}

internal class HomeSlideshowSettings
{
	public List<int> ImageIds { get; set; } = new();
	public int? IntervalMs { get; set; }
}

internal static class HomeSlideshowSettingsHelper
{
	public const string SettingKey = "home-slideshow-image-ids";
	public const string IntroVideoSettingKey = "home-slideshow-intro-video-url";
	public const int DefaultIntervalMs = 4500;
	public const int MinIntervalMs = 1000;
	public const int MaxIntervalMs = 30000;
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static IQueryable<PortfolioImage> GetAvailableImagesQuery(AppDbContext context) =>
		context.PortfolioImages
			.Include(x => x.PortfolioAlbum!)
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

	public static SlideshowImageDto ToDto(PortfolioImage image, bool isSelected = false, int? slideshowOrder = null) => new()
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

	public static async Task<HomeSlideshowSettings> GetSettingsAsync(AppDbContext context)
	{
		var setting = await context.SiteSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == SettingKey);
		if (setting == null || string.IsNullOrWhiteSpace(setting.Value)) return new HomeSlideshowSettings();

		try
		{
			var parsed = JsonSerializer.Deserialize<HomeSlideshowSettings>(setting.Value, JsonOptions) ?? new HomeSlideshowSettings();
			parsed.ImageIds = NormalizeIds(parsed.ImageIds);
			parsed.IntervalMs = NormalizeIntervalMs(parsed.IntervalMs);
			return parsed;
		}
		catch
		{
			try
			{
				var parsedIds = JsonSerializer.Deserialize<List<int>>(setting.Value, JsonOptions);
				return new HomeSlideshowSettings { ImageIds = NormalizeIds(parsedIds) };
			}
			catch
			{
				return new HomeSlideshowSettings();
			}
		}
	}

	public static async Task<List<int>> GetSelectedImageIdsAsync(AppDbContext context)
	{
		var settings = await GetSettingsAsync(context);
		return NormalizeIds(settings.ImageIds);
	}

	public static int GetEffectiveIntervalMs(HomeSlideshowSettings settings) =>
		NormalizeIntervalMs(settings.IntervalMs) ?? DefaultIntervalMs;

	public static int? NormalizeIntervalMs(int? value)
	{
		if (!value.HasValue) return null;
		return Math.Clamp(value.Value, MinIntervalMs, MaxIntervalMs);
	}

	public static async Task SaveSettingsAsync(AppDbContext context, HomeSlideshowSettings settings)
	{
		settings.ImageIds = NormalizeIds(settings.ImageIds);
		settings.IntervalMs = NormalizeIntervalMs(settings.IntervalMs);

		var value = JsonSerializer.Serialize(settings, JsonOptions);
		var setting = await context.SiteSettings.FirstOrDefaultAsync(x => x.Key == SettingKey);

		if (setting == null)
		{
			context.SiteSettings.Add(new SiteSetting
			{
				Key = SettingKey,
				Value = value,
				Description = "Selected image ids, order and timing for the home page slideshow.",
				UpdatedAtUtc = DateTime.UtcNow
			});
			return;
		}

		setting.Value = value;
		setting.Description = "Selected image ids, order and timing for the home page slideshow.";
		setting.UpdatedAtUtc = DateTime.UtcNow;
	}

	public static async Task SaveSelectedImageIdsAsync(AppDbContext context, List<int> imageIds)
	{
		var settings = await GetSettingsAsync(context);
		settings.ImageIds = NormalizeIds(imageIds);
		await SaveSettingsAsync(context, settings);
	}

	public static SlideshowSettingsResponse ToSettingsResponse(HomeSlideshowSettings settings) => new()
	{
		UseDefaultInterval = !settings.IntervalMs.HasValue,
		IntervalMs = GetEffectiveIntervalMs(settings),
		DefaultIntervalMs = DefaultIntervalMs,
		MinIntervalMs = MinIntervalMs,
		MaxIntervalMs = MaxIntervalMs
	};

	public static async Task<string?> GetIntroVideoUrlAsync(AppDbContext context)
	{
		var setting = await context.SiteSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == IntroVideoSettingKey);
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
			return;
		}

		setting.Value = normalizedUrl;
		setting.Description = "Optional intro video shown once before the home page slideshow.";
		setting.UpdatedAtUtc = DateTime.UtcNow;
	}

	public static List<int> NormalizeIds(IEnumerable<int>? ids)
	{
		var result = new List<int>();
		var seen = new HashSet<int>();

		foreach (var id in ids ?? Enumerable.Empty<int>())
		{
			if (id > 0 && seen.Add(id)) result.Add(id);
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
			var defaultItems = await HomeSlideshowSettingsHelper.ApplyDefaultOrder(query).ToListAsync();
			return Ok(defaultItems.Select(x => HomeSlideshowSettingsHelper.ToDto(x)).ToList());
		}

		var selectedSet = selectedIds.ToHashSet();
		var selectedImages = await query.Where(x => selectedSet.Contains(x.Id)).ToListAsync();
		var selectedById = selectedImages.ToDictionary(x => x.Id);
		var orderedItems = selectedIds
			.Select((id, index) => selectedById.TryGetValue(id, out var image) ? HomeSlideshowSettingsHelper.ToDto(image, true, index + 1) : null)
			.OfType<SlideshowImageDto>()
			.ToList();

		return Ok(orderedItems);
	}

	[HttpGet("intro-video")]
	public async Task<IActionResult> GetIntroVideo()
	{
		return Ok(new SlideshowIntroVideoResponse { VideoUrl = await HomeSlideshowSettingsHelper.GetIntroVideoUrlAsync(_context) });
	}

	[HttpGet("settings")]
	public async Task<IActionResult> GetSettings()
	{
		var settings = await HomeSlideshowSettingsHelper.GetSettingsAsync(_context);
		return Ok(HomeSlideshowSettingsHelper.ToSettingsResponse(settings));
	}
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/slideshow")]
public class AdminSlideshowController : ControllerBase
{
	private const long MaxIntroVideoUploadSizeBytes = 100 * 1024 * 1024;
	private const long MaxIntroVideoUploadRequestSizeBytes = 105 * 1024 * 1024;
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
		var availableImages = await HomeSlideshowSettingsHelper.ApplyDefaultOrder(HomeSlideshowSettingsHelper.GetAvailableImagesQuery(_context).AsNoTracking()).ToListAsync();
		var settings = await HomeSlideshowSettingsHelper.GetSettingsAsync(_context);
		var savedIds = HomeSlideshowSettingsHelper.NormalizeIds(settings.ImageIds);
		var currentIds = savedIds.Count > 0 ? savedIds : availableImages.Select(x => x.Id).ToList();
		var availableById = availableImages.ToDictionary(x => x.Id);
		var currentIdSet = currentIds.ToHashSet();
		var selectedImages = currentIds
			.Select((id, index) => availableById.TryGetValue(id, out var image) ? HomeSlideshowSettingsHelper.ToDto(image, true, index + 1) : null)
			.OfType<SlideshowImageDto>()
			.ToList();
		var allImages = availableImages.Select(image =>
		{
			var selectedIndex = currentIds.IndexOf(image.Id);
			return HomeSlideshowSettingsHelper.ToDto(image, currentIdSet.Contains(image.Id), selectedIndex >= 0 ? selectedIndex + 1 : null);
		}).ToList();
		var timing = HomeSlideshowSettingsHelper.ToSettingsResponse(settings);

		return Ok(new AdminSlideshowResponse
		{
			SelectedImages = selectedImages,
			AvailableImages = allImages,
			ImageIds = selectedImages.Select(x => x.Id).ToList(),
			IntroVideoUrl = await HomeSlideshowSettingsHelper.GetIntroVideoUrlAsync(_context),
			UseDefaultInterval = timing.UseDefaultInterval,
			IntervalMs = timing.IntervalMs,
			DefaultIntervalMs = timing.DefaultIntervalMs,
			MinIntervalMs = timing.MinIntervalMs,
			MaxIntervalMs = timing.MaxIntervalMs
		});
	}

	[HttpPut]
	public async Task<IActionResult> UpdateSlideshow([FromBody] UpdateHomeSlideshowRequest model)
	{
		var requestedIds = HomeSlideshowSettingsHelper.NormalizeIds(model.ImageIds);
		var availableIds = await HomeSlideshowSettingsHelper.GetAvailableImagesQuery(_context)
			.AsNoTracking()
			.Where(x => requestedIds.Contains(x.Id))
			.Select(x => x.Id)
			.ToListAsync();

		var availableSet = availableIds.ToHashSet();
		var settings = await HomeSlideshowSettingsHelper.GetSettingsAsync(_context);
		settings.ImageIds = requestedIds.Where(availableSet.Contains).ToList();

		if (model.UseDefaultInterval == true)
		{
			settings.IntervalMs = null;
		}
		else if (model.UseDefaultInterval == false && model.IntervalMs.HasValue)
		{
			settings.IntervalMs = HomeSlideshowSettingsHelper.NormalizeIntervalMs(model.IntervalMs);
		}
		else if (model.IntervalMs.HasValue)
		{
			settings.IntervalMs = HomeSlideshowSettingsHelper.NormalizeIntervalMs(model.IntervalMs);
		}

		await HomeSlideshowSettingsHelper.SaveSettingsAsync(_context, settings);
		await _context.SaveChangesAsync();
		return NoContent();
	}

	[HttpPost("video")]
	[RequestSizeLimit(MaxIntroVideoUploadRequestSizeBytes)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxIntroVideoUploadRequestSizeBytes)]
	public async Task<IActionResult> UploadIntroVideo([FromForm] IFormFile file)
	{
		if (file == null || file.Length == 0) return BadRequest(new { message = "Избери видео файл." });
		if (file.Length > MaxIntroVideoUploadSizeBytes) return BadRequest(new { message = "Видеото е твърде голямо. Максимумът е 100MB." });
		if (!IsAllowedVideo(file)) return BadRequest(new { message = "Позволени са само видео файлове: mp4, mov, webm, m4v." });

		var webRoot = _environment.WebRootPath;
		if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

		var relativeDirectory = Path.Combine("uploads", "portfolio");
		var directory = Path.Combine(webRoot, relativeDirectory);
		Directory.CreateDirectory(directory);

		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var fileName = $"home-intro-{Guid.NewGuid():N}{extension}";
		var path = Path.Combine(directory, fileName);

		await using (var stream = System.IO.File.Create(path))
		{
			await file.CopyToAsync(stream);
		}

		var relativeUrl = $"/{relativeDirectory.Replace("\\", "/")}/{fileName}";
		await HomeSlideshowSettingsHelper.SaveIntroVideoUrlAsync(_context, relativeUrl);
		await _context.SaveChangesAsync();

		return Ok(new SlideshowIntroVideoResponse { VideoUrl = relativeUrl });
	}

	[HttpDelete("video")]
	public async Task<IActionResult> DeleteIntroVideo()
	{
		await HomeSlideshowSettingsHelper.SaveIntroVideoUrlAsync(_context, null);
		await _context.SaveChangesAsync();
		return NoContent();
	}

	private static bool IsAllowedVideo(IFormFile file)
	{
		var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".webm", ".m4v" };
		var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "video/mp4", "video/quicktime", "video/webm", "video/x-m4v" };
		var extension = Path.GetExtension(file.FileName);
		return allowedExtensions.Contains(extension) || allowedContentTypes.Contains(file.ContentType ?? string.Empty);
	}
}