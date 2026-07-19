using System.Text.Json;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowSettingsService(AppDbContext context)
{
    private const string SettingKey = "home-slideshow-image-ids";
    private const int DefaultIntervalMs = 4500;
    private const int MinIntervalMs = 1000;
    private const int MaxIntervalMs = 30000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal async Task<HomeSlideshowSettings> LoadAsync()
    {
        var setting = await context.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == SettingKey);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            return new HomeSlideshowSettings();

        try
        {
            var parsed = JsonSerializer.Deserialize<HomeSlideshowSettings>(setting.Value, JsonOptions)
                ?? new HomeSlideshowSettings();
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

    public async Task<List<int>> GetSelectedImageIdsAsync()
    {
        var settings = await LoadAsync();
        return NormalizeIds(settings.ImageIds);
    }

    public async Task<SlideshowSettingsResponse> GetResponseAsync() =>
        ToResponse(await LoadAsync());

    public async Task UpdateAsync(
        UpdateHomeSlideshowRequest request,
        IReadOnlyCollection<int> availableIds)
    {
        var availableSet = availableIds.ToHashSet();
        var settings = await LoadAsync();
        settings.ImageIds = NormalizeIds(request.ImageIds)
            .Where(availableSet.Contains)
            .ToList();

        if (request.UseDefaultInterval == true)
        {
            settings.IntervalMs = null;
        }
        else if (request.IntervalMs.HasValue)
        {
            settings.IntervalMs = NormalizeIntervalMs(request.IntervalMs);
        }

        await SaveAsync(settings);
        await context.SaveChangesAsync();
    }

    internal static SlideshowSettingsResponse ToResponse(HomeSlideshowSettings settings) => new()
    {
        UseDefaultInterval = !settings.IntervalMs.HasValue,
        IntervalMs = NormalizeIntervalMs(settings.IntervalMs) ?? DefaultIntervalMs,
        DefaultIntervalMs = DefaultIntervalMs,
        MinIntervalMs = MinIntervalMs,
        MaxIntervalMs = MaxIntervalMs
    };

    internal static List<int> NormalizeIds(IEnumerable<int>? ids)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();

        foreach (var id in ids ?? Enumerable.Empty<int>())
        {
            if (id > 0 && seen.Add(id))
                result.Add(id);
        }

        return result;
    }

    private static int? NormalizeIntervalMs(int? value) =>
        value.HasValue
            ? Math.Clamp(value.Value, MinIntervalMs, MaxIntervalMs)
            : null;

    private async Task SaveAsync(HomeSlideshowSettings settings)
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
}