using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowService : IHomeSlideshowService
{
    private readonly HomeSlideshowImageService _images;
    private readonly HomeSlideshowSettingsService _settings;
    private readonly HomeSlideshowVideoService _video;

    [ActivatorUtilitiesConstructor]
    public HomeSlideshowService(
        HomeSlideshowImageService images,
        HomeSlideshowSettingsService settings,
        HomeSlideshowVideoService video)
    {
        _images = images;
        _settings = settings;
        _video = video;
    }

    public HomeSlideshowService(
        AppDbContext context,
        IWebHostEnvironment environment)
        : this(
            new HomeSlideshowImageService(
                context,
                new HomeSlideshowSettingsService(context),
                new HomeSlideshowVideoService(context, environment)),
            new HomeSlideshowSettingsService(context),
            new HomeSlideshowVideoService(context, environment))
    {
    }

    public Task<IReadOnlyList<SlideshowImageDto>> GetSlideshowImagesAsync() =>
        _images.GetSlideshowImagesAsync();

    public Task<SlideshowIntroVideoResponse> GetIntroVideoAsync() =>
        _video.GetAsync();

    public Task<SlideshowSettingsResponse> GetSettingsAsync() =>
        _settings.GetResponseAsync();

    public Task<AdminSlideshowResponse> GetManagementAsync() =>
        _images.GetManagementAsync();

    public Task UpdateAsync(UpdateHomeSlideshowRequest request) =>
        _images.UpdateAsync(request);

    public Task<SlideshowIntroVideoResponse> UploadIntroVideoAsync(IFormFile file) =>
        _video.UploadAsync(file);

    public Task DeleteIntroVideoAsync() =>
        _video.DeleteAsync();
}