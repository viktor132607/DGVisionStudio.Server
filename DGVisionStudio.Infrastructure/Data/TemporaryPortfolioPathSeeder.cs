using DGVisionStudio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class TemporaryPortfolioPathSeeder
{
    private const string OldPrefix = "/images/portfolio/";
    private const string CorrectPrefix = "/images/porfolio/";

    private static readonly string[] AdditionalRepositoryImages =
    {
        "/images/porfolio/events/bulgare/2.jpg",
        "/images/porfolio/events/bulgare/3.jpg"
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var albums = await db.PortfolioAlbums
            .IgnoreQueryFilters()
            .Where(x => x.CoverImageUrl != null && x.CoverImageUrl.StartsWith(OldPrefix))
            .ToListAsync();

        foreach (var album in albums)
        {
            album.CoverImageUrl = FixPath(album.CoverImageUrl);
        }

        var images = await db.PortfolioImages
            .IgnoreQueryFilters()
            .Where(x => x.ImageUrl.StartsWith(OldPrefix)
                || (x.ThumbnailUrl != null && x.ThumbnailUrl.StartsWith(OldPrefix)))
            .ToListAsync();

        foreach (var image in images)
        {
            image.ImageUrl = FixPath(image.ImageUrl)!;
            image.ThumbnailUrl = FixPath(image.ThumbnailUrl);
        }

        var serviceItems = await db.Services
            .Where(x => x.CoverImageUrl != null && x.CoverImageUrl.StartsWith(OldPrefix))
            .ToListAsync();

        foreach (var service in serviceItems)
        {
            service.CoverImageUrl = FixPath(service.CoverImageUrl);
        }

        var settings = await db.SiteSettings
            .Where(x => x.Value.Contains(OldPrefix))
            .ToListAsync();

        foreach (var setting in settings)
        {
            setting.Value = setting.Value.Replace(OldPrefix, CorrectPrefix, StringComparison.OrdinalIgnoreCase);
        }

        await db.SaveChangesAsync();

        var activePortfolioImages = await db.PortfolioImages
            .Where(x => x.ImageUrl.StartsWith(CorrectPrefix))
            .OrderBy(x => x.Id)
            .ToListAsync();

        var duplicates = activePortfolioImages
            .GroupBy(x => $"{x.PortfolioAlbumId}|{x.ImageUrl}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1))
            .ToList();

        if (duplicates.Count > 0)
        {
            db.PortfolioImages.RemoveRange(duplicates);
            await db.SaveChangesAsync();
        }

        // These two files are committed in the current repository in addition to the
        // historical full Bulgare seed list, so keep them in the album as well.
        var eventAlbum = await db.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Slug == "event-bulgare");

        if (eventAlbum != null)
        {
            var nextOrder = eventAlbum.Images.Count == 0
                ? 1
                : eventAlbum.Images.Max(x => x.DisplayOrder) + 1;

            foreach (var path in AdditionalRepositoryImages)
            {
                if (eventAlbum.Images.Any(x => string.Equals(x.ImageUrl, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                db.PortfolioImages.Add(new PortfolioImage
                {
                    PortfolioAlbumId = eventAlbum.Id,
                    ImageUrl = path,
                    ThumbnailUrl = path,
                    AltText = $"{eventAlbum.Title} {nextOrder}",
                    DisplayOrder = nextOrder++,
                    IsCover = false,
                    IsPublished = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }
    }

    private static string? FixPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Replace(OldPrefix, CorrectPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
