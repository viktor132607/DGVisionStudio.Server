namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowImageManagementService(
    HomeSlideshowImageCatalogService catalog,
    HomeSlideshowSettingsService settingsService,
    HomeSlideshowVideoService videoService,
    HomeSlideshowImageMapper mapper)
{
    public async Task<AdminSlideshowResponse>
        GetManagementAsync()
    {
        var availableImages =
            await catalog.GetAvailableAsync(
                includeScheduled: true);

        var settings =
            await settingsService.LoadAsync();

        var savedIds =
            HomeSlideshowSettingsService.NormalizeIds(
                settings.ImageIds);

        var requestedIds =
            savedIds.Count > 0
                ? savedIds
                : availableImages
                    .Select(image => image.Id)
                    .ToList();

        var availableById =
            availableImages.ToDictionary(
                image => image.Id);

        var currentIds =
            requestedIds
                .Where(availableById.ContainsKey)
                .ToList();

        var currentIdSet =
            currentIds.ToHashSet();

        var selectedImages =
            currentIds
                .Select((id, index) =>
                    mapper.Map(
                        availableById[id],
                        true,
                        index + 1))
                .ToList();

        var allImages =
            availableImages
                .Select(image =>
                {
                    var selectedIndex =
                        currentIds.IndexOf(image.Id);

                    return mapper.Map(
                        image,
                        currentIdSet.Contains(image.Id),
                        selectedIndex >= 0
                            ? selectedIndex + 1
                            : null);
                })
                .ToList();

        var timing =
            HomeSlideshowSettingsService.ToResponse(
                settings);

        return new AdminSlideshowResponse
        {
            SelectedImages = selectedImages,
            AvailableImages = allImages,
            ImageIds = selectedImages
                .Select(image => image.Id)
                .ToList(),
            IntroVideoUrl =
                await videoService.GetUrlAsync(),
            UseDefaultInterval =
                timing.UseDefaultInterval,
            IntervalMs = timing.IntervalMs,
            DefaultIntervalMs =
                timing.DefaultIntervalMs,
            MinIntervalMs = timing.MinIntervalMs,
            MaxIntervalMs = timing.MaxIntervalMs
        };
    }

    public async Task UpdateAsync(
        UpdateHomeSlideshowRequest request)
    {
        var requestedIds =
            HomeSlideshowSettingsService.NormalizeIds(
                request.ImageIds);

        var availableIds =
            await catalog.GetAvailableIdsAsync(
                requestedIds,
                includeScheduled: true);

        await settingsService.UpdateAsync(
            request,
            availableIds);
    }
}
