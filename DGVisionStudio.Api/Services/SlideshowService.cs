using System.Text.Json;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public interface IHomeSlideshowService
{
	Task<IReadOnlyList<SlideshowImageDto>> GetSlideshowImagesAsync();
	Task<SlideshowIntroVideoResponse> GetIntroVideoAsync();
	Task<SlideshowSettingsResponse> GetSettingsAsync();
	Task<AdminSlideshowResponse> GetManagementAsync();
	Task UpdateAsync(UpdateHomeSlideshowRequest request);
	Task<SlideshowIntroVideoResponse> UploadIntroVideoAsync(IFormFile file);
	Task DeleteIntroVideoAsync();
}

public sealed class HomeSlideshowService(
	AppDbContext context,
	IWebHostEnvironment environment) : IHomeSlideshowService
{
	private const long MaxIntroVideoUploadSizeBytes = 100 * 1024 * 1024;
	private const string SettingKey = "home-slideshow-image-ids";
	private const string IntroVideoSettingKey = "home-slideshow-intro-video-url";
	private const int DefaultIntervalMs = 4500;
	private const int MinIntervalMs = 1000;
	private const int MaxIntervalMs = 30000;
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<IReadOnlyList<SlideshowImageDto>> GetSlideshowImagesAsync()
	{
		var selectedIds = await GetSelectedImageIdsAsync();
		var query = GetAvailableImagesQuery().AsNoTracking();

		if (selectedIds.Count == 0)
		{
			var defaultItems = await ApplyDefaultOrder(query).ToListAsync();
			return defaultItems.Select(x => ToDto(x)).ToList();
		}

		var selectedSet = selectedIds.ToHashSet();
		var selectedImages = await query.Where(x => selectedSet.Contains(x.Id)).ToListAsync();
		var selectedById = selectedImages.ToDictionary(x => x.Id);

		return selectedIds
			.Select((id, index) => selectedById.TryGetValue(id, out var image) ? ToDto(image, true, index + 1) : null)
			.OfType<SlideshowImageDto>()
			.ToList();
	}

	public async Task<SlideshowIntroVideoResponse> GetIntroVideoAsync()
	{
		return new SlideshowIntroVideoResponse { VideoUrl = await GetIntroVideoUrlAsync() };
	}

	public async Task<SlideshowSettingsResponse> GetSettingsAsync()
	{
		var settings = await LoadSettingsAsync();
		return ToSettingsResponse(settings);
	}

	public async Task<AdminSlideshowResponse> GetManagementAsync()
	{
		var availableImages = await ApplyDefaultOrder(GetAvailableImagesQuery().AsNoTracking()).ToListAsync();
		var settings = await LoadSettingsAsync();
		var savedIds = NormalizeIds(settings.ImageIds);
		var currentIds = savedIds.Count > 0 ? savedIds : availableImages.Select(x => x.Id).ToList();
		var availableById = availableImages.ToDictionary(x => x.Id);
		var currentIdSet = currentIds.ToHashSet();

		var selectedImages = currentIds
			.Select((id, index) => availableById.TryGetValue(id, out var image) ? ToDto(image, true, index + 1) : null)
			.OfType<SlideshowImageDto>()
			.ToList();

		var allImages = availableImages.Select(image =>
		{
			var selectedIndex = currentIds.IndexOf(image.Id);
			return ToDto(image, currentIdSet.Contains(image.Id), selectedIndex >= 0 ? selectedIndex + 1 : null);
		}).ToList();

		var timing = ToSettingsResponse(settings);

		return new AdminSlideshowResponse
		{
			SelectedImages = selectedImages,
			AvailableImages = allImages,
			ImageIds = selectedImages.Select(x => x.Id).ToList(),
			IntroVideoUrl = await GetIntroVideoUrlAsync(),
			UseDefaultInterval = timing.UseDefaultInterval,
			IntervalMs = timing.IntervalMs,
			DefaultIntervalMs = timing.DefaultIntervalMs,
			MinIntervalMs = timing.MinIntervalMs,
			MaxIntervalMs = timing.MaxIntervalMs
		};
	}

	public async Task UpdateAsync(UpdateHomeSlideshowRequest request)
	{
		var requestedIds = NormalizeIds(request.ImageIds);
		var availableIds = await GetAvailableImagesQuery()
			.AsNoTracking()
			.Where(x => requestedIds.Contains(x.Id))
			.Select(x => x.Id)
			.ToListAsync();

		var availableSet = availableIds.ToHashSet();
		var settings = await LoadSettingsAsync();
		settings.ImageIds = requestedIds.Where(availableSet.Contains).ToList();

		if (request.UseDefaultInterval == true)
		{
			settings.IntervalMs = null;
		}
		else if (request.UseDefaultInterval == false && request.IntervalMs.HasValue)
		{
			settings.IntervalMs = NormalizeIntervalMs(request.IntervalMs);
		}
		else if (request.IntervalMs.HasValue)
		{
			settings.IntervalMs = NormalizeIntervalMs(request.IntervalMs);
		}

		await SaveSettingsAsync(settings);
		await context.SaveChangesAsync();
	}

	public async Task<SlideshowIntroVideoResponse> UploadIntroVideoAsync(IFormFile file)
	{
		if (file == null || file.Length == 0)
			throw new SlideshowValidationException("Избери видео файл.");

		if (file.Length > MaxIntroVideoUploadSizeBytes)
			throw new SlideshowValidationException("Видеото е твърде голямо. Максимумът е 100MB.");

		if (!IsAllowedVideo(file))
			throw new SlideshowValidationException("Позволени са само видео файлове: mp4, mov, webm, m4v.");

		var webRoot = environment.WebRootPath;
		if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");

		var relativeDirectory = Path.Combine("uploads", "portfolio");
		var directory = Path.Combine(webRoot, relativeDirectory);
		Directory.CreateDirectory(directory);

		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
		var fileName = $"home-intro-{Guid.NewGuid():N}{extension}";
		var path = Path.Combine(directory, fileName);

		await using (var stream = File.Create(path))
		{
			await file.CopyToAsync(stream);
		}

		var relativeUrl = $"/{relativeDirectory.Replace("\\", "/")}/{fileName}";
		await SaveIntroVideoUrlAsync(relativeUrl);
		await context.SaveChangesAsync();

		return new SlideshowIntroVideoResponse { VideoUrl = relativeUrl };
	}

	public async Task DeleteIntroVideoAsync()
	{
		await SaveIntroVideoUrlAsync(null);
		await context.SaveChangesAsync();
	}

	private IQueryable<PortfolioImage> GetAvailableImagesQuery() =>
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

	private static IQueryable<PortfolioImage> ApplyDefaultOrder(IQueryable<PortfolioImage> query) =>
		query
			.OrderBy(x => x.PortfolioAlbum!.PortfolioCategory!.DisplayOrder)
			.ThenBy(x => x.PortfolioAlbum!.DisplayOrder)
			.ThenBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id);

	private static SlideshowImageDto ToDto(PortfolioImage image, bool isSelected = false, int? slideshowOrder = null) => new()
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

	private async Task<HomeSlideshowSettings> LoadSettingsAsync()
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

	private async Task<List<int>> GetSelectedImageIdsAsync()
	{
		var settings = await LoadSettingsAsync();
		return NormalizeIds(settings.ImageIds);
	}

	private static int GetEffectiveIntervalMs(HomeSlideshowSettings settings) =>
		NormalizeIntervalMs(settings.IntervalMs) ?? DefaultIntervalMs;

	private static int? NormalizeIntervalMs(int? value)
	{
		if (!value.HasValue) return null;
		return Math.Clamp(value.Value, MinIntervalMs, MaxIntervalMs);
	}

	private async Task SaveSettingsAsync(HomeSlideshowSettings settings)
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

	private static SlideshowSettingsResponse ToSettingsResponse(HomeSlideshowSettings settings) => new()
	{
		UseDefaultInterval = !settings.IntervalMs.HasValue,
		IntervalMs = GetEffectiveIntervalMs(settings),
		DefaultIntervalMs = DefaultIntervalMs,
		MinIntervalMs = MinIntervalMs,
		MaxIntervalMs = MaxIntervalMs
	};

	private async Task<string?> GetIntroVideoUrlAsync()
	{
		var setting = await context.SiteSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == IntroVideoSettingKey);
		return string.IsNullOrWhiteSpace(setting?.Value) ? null : setting.Value.Trim();
	}

	private async Task SaveIntroVideoUrlAsync(string? videoUrl)
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

	private static List<int> NormalizeIds(IEnumerable<int>? ids)
	{
		var result = new List<int>();
		var seen = new HashSet<int>();

		foreach (var id in ids ?? Enumerable.Empty<int>())
		{
			if (id > 0 && seen.Add(id)) result.Add(id);
		}

		return result;
	}

	private static bool IsAllowedVideo(IFormFile file)
	{
		var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".webm", ".m4v" };
		var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "video/mp4", "video/quicktime", "video/webm", "video/x-m4v" };
		var extension = Path.GetExtension(file.FileName);
		return allowedExtensions.Contains(extension) || allowedContentTypes.Contains(file.ContentType ?? string.Empty);
	}
}

public sealed class SlideshowValidationException(string message) : Exception(message);

public sealed class SlideshowImageDto
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

public sealed class AdminSlideshowResponse
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

public sealed class SlideshowIntroVideoResponse
{
	public string? VideoUrl { get; set; }
}

public sealed class SlideshowSettingsResponse
{
	public bool UseDefaultInterval { get; set; }
	public int IntervalMs { get; set; }
	public int DefaultIntervalMs { get; set; }
	public int MinIntervalMs { get; set; }
	public int MaxIntervalMs { get; set; }
}

public sealed class UpdateHomeSlideshowRequest
{
	public List<int>? ImageIds { get; set; }
	public bool? UseDefaultInterval { get; set; }
	public int? IntervalMs { get; set; }
}

internal sealed class HomeSlideshowSettings
{
	public List<int> ImageIds { get; set; } = new();
	public int? IntervalMs { get; set; }
}
