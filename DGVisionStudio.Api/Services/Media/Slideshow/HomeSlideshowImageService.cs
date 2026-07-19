using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class HomeSlideshowImageService(
    AppDbContext context,
    HomeSlideshowSettingsService settingsService,
    HomeSlideshowVideoService videoService)
{
    public async Task<IReadOnlyList<SlideshowImageDto>> GetSlideshowImagesAsync()
    {
        var selectedIds = await settingsService.GetSelectedImageIdsAsync();
        var query = GetAvailableImagesQuery().AsNoTracking();

        if (selectedIds.Count == 0)
        {
            var defaultItems = await ApplyDefaultOrder(query).ToListAsync();
            return defaultItems.Select(x => ToDto(x)).ToList();
        }

        var selectedSet = selectedIds.ToHashSet();
        var selectedImages = await query
            .Where(x => selectedSet.Contains(x.Id))
            .ToListAsync();
        var selectedById = selectedImages.ToDictionary(x => x.Id);

        return selectedIds
            .Select((id, index) => selectedById.TryGetValue(id, out var image)
                ? ToDto(image, true, index + 1)
                : null)
            .OfType<SlideshowImageDto>()
            .ToList();
    }

    public async Task<AdminSlideshowResponse> GetManagementAsync()
    {
        var availableImages = await ApplyDefaultOrder(GetAvailableImagesQuery().AsNoTracking())
            .ToListAsync();
        var settings = await settingsService.LoadAsync();
        var savedIds = HomeSlideshowSettingsService.NormalizeIds(settings.ImageIds);
        var currentIds = savedIds.Count > 0
            ? savedIds
            : availableImages.Select(x => x.Id).ToList();
        var availableById = availableImages.ToDictionary(x => x.Id);
        var currentIdSet = currentIds.ToHashSet();

        var selectedImages = currentIds
            .Select((id, index) => availableById.TryGetValue(id, out var image)
                ? ToDto(image, true, index + 1)
                : null)
            .OfType<SlideshowImageDto>()
            .ToList();

        var allImages = availableImages.Select(image =>
        {
            var selectedIndex = currentIds.IndexOf(image.Id);
            return ToDto(
                image,
                currentIdSet.Contains(image.Id),
                selectedIndex >= 0 ? selectedIndex + 1 : null);
        }).ToList();

        var timing = HomeSlideshowSettingsService.ToResponse(settings);

        return new AdminSlideshowResponse
        {
            SelectedImages = selectedImages,
            AvailableImages = allImages,
            ImageIds = selectedImages.Select(x => x.Id).ToList(),
            IntroVideoUrl = await videoService.GetUrlAsync(),
            UseDefaultInterval = timing.UseDefaultInterval,
            IntervalMs = timing.IntervalMs,
            DefaultIntervalMs = timing.DefaultIntervalMs,
            MinIntervalMs = timing.MinIntervalMs,
            MaxIntervalMs = timing.MaxIntervalMs
        };
    }

    public async Task UpdateAsync(UpdateHomeSlideshowRequest request)
    {
        var requestedIds = HomeSlideshowSettingsService.NormalizeIds(request.ImageIds);
        var availableIds = await GetAvailableImagesQuery()
            .AsNoTracking()
            .Where(x => requestedIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        await settingsService.UpdateAsync(request, availableIds);
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

    private static SlideshowImageDto ToDto(
        PortfolioImage image,
        bool isSelected = false,
        int? slideshowOrder = null) => new()
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
}