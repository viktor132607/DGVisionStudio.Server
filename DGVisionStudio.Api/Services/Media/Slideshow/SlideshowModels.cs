using Microsoft.AspNetCore.Http;

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
    public List<SlideshowImageDto> SelectedImages { get; set; } = [];
    public List<SlideshowImageDto> AvailableImages { get; set; } = [];
    public List<int> ImageIds { get; set; } = [];
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
    public List<int> ImageIds { get; set; } = [];
    public int? IntervalMs { get; set; }
}