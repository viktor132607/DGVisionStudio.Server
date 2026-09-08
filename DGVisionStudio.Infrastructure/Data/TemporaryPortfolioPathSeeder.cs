using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class TemporaryPortfolioPathSeeder
{
    private const string OldPrefix = "/images/portfolio/";
    private const string CorrectPrefix = "/images/porfolio/";

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

        // AppDataSeeder historically used /images/portfolio/. On subsequent starts it can
        // temporarily insert the same image again before this repair runs, so keep the
        // repaired data idempotent by removing active duplicates after path normalization.
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
