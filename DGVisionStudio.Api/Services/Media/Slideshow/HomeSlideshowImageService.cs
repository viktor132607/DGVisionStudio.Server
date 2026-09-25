using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowImageService
{
    private readonly HomeSlideshowImageSelectionService _selection;
    private readonly HomeSlideshowImageManagementService _management;

    [ActivatorUtilitiesConstructor]
    public HomeSlideshowImageService(
        HomeSlideshowImageSelectionService selection,
        HomeSlideshowImageManagementService management)
    {
        _selection = selection;
        _management = management;
    }

    public HomeSlideshowImageService(
        AppDbContext context,
        HomeSlideshowSettingsService settingsService,
        HomeSlideshowVideoService videoService)
        : this(
            new HomeSlideshowImageSelectionService(
                new HomeSlideshowImageCatalogService(context),
                settingsService,
                new HomeSlideshowImageMapper()),
            new HomeSlideshowImageManagementService(
                new HomeSlideshowImageCatalogService(context),
                settingsService,
                videoService,
                new HomeSlideshowImageMapper()))
    {
    }

    public Task<IReadOnlyList<SlideshowImageDto>>
        GetSlideshowImagesAsync() =>
        _selection.GetSlideshowImagesAsync();

    public Task<AdminSlideshowResponse>
        GetManagementAsync() =>
        _management.GetManagementAsync();

    public Task UpdateAsync(
        UpdateHomeSlideshowRequest request) =>
        _management.UpdateAsync(request);
}
