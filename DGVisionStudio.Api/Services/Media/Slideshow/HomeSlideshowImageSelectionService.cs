namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowImageSelectionService(
    HomeSlideshowImageCatalogService catalog,
    HomeSlideshowSettingsService settings,
    HomeSlideshowImageMapper mapper)
{
    public async Task<IReadOnlyList<SlideshowImageDto>>
        GetSlideshowImagesAsync()
    {
        var selectedIds =
            await settings.GetSelectedImageIdsAsync();

        if (selectedIds.Count == 0)
        {
            var defaultItems =
                await catalog.GetAvailableAsync();

            return defaultItems
                .Select(image => mapper.Map(image))
                .ToList();
        }

        var selectedImages =
            await catalog.GetByIdsAsync(selectedIds);
        var selectedById =
            selectedImages.ToDictionary(
                image => image.Id);

        return selectedIds
            .Select((id, index) =>
                selectedById.TryGetValue(
                    id,
                    out var image)
                    ? mapper.Map(
                        image,
                        true,
                        index + 1)
                    : null)
            .OfType<SlideshowImageDto>()
            .ToList();
    }
}
