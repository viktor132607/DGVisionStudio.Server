using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowVideoService(
    AppDbContext context,
    IWebHostEnvironment environment)
{
    private const long MaxIntroVideoUploadSizeBytes = 100 * 1024 * 1024;
    private const string IntroVideoSettingKey = "home-slideshow-intro-video-url";

    public async Task<SlideshowIntroVideoResponse> GetAsync() =>
        new() { VideoUrl = await GetUrlAsync() };

    internal async Task<string?> GetUrlAsync()
    {
        var setting = await context.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == IntroVideoSettingKey);

        return string.IsNullOrWhiteSpace(setting?.Value)
            ? null
            : setting.Value.Trim();
    }

    public async Task<SlideshowIntroVideoResponse> UploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new SlideshowValidationException("Избери видео файл.");

        if (file.Length > MaxIntroVideoUploadSizeBytes)
            throw new SlideshowValidationException("Видеото е твърде голямо. Максимумът е 100MB.");

        if (!IsAllowedVideo(file))
            throw new SlideshowValidationException("Позволени са само видео файлове: mp4, mov, webm, m4v.");

        var webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");

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
        await SaveUrlAsync(relativeUrl);
        await context.SaveChangesAsync();

        return new SlideshowIntroVideoResponse { VideoUrl = relativeUrl };
    }

    public async Task DeleteAsync()
    {
        await SaveUrlAsync(null);
        await context.SaveChangesAsync();
    }

    private async Task SaveUrlAsync(string? videoUrl)
    {
        var setting = await context.SiteSettings.FirstOrDefaultAsync(x => x.Key == IntroVideoSettingKey);
        var normalizedUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();

        if (normalizedUrl == null)
        {
            if (setting != null)
                context.SiteSettings.Remove(setting);
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

    private static bool IsAllowedVideo(IFormFile file)
    {
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".mov",
            ".webm",
            ".m4v"
        };
        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime",
            "video/webm",
            "video/x-m4v"
        };
        var extension = Path.GetExtension(file.FileName);

        return allowedExtensions.Contains(extension) ||
            allowedContentTypes.Contains(file.ContentType ?? string.Empty);
    }
}