using System.Text.Json;
using DGVisionStudio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class PhotographyPageSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        // Tombstones prevent deleted content from being recreated on restart.
        if (await db.PhotographyPages.IgnoreQueryFilters().AnyAsync()) return;
        using var stream = typeof(PhotographyPageSeeder).Assembly.GetManifestResourceStream(
            "DGVisionStudio.Infrastructure.Data.PhotographyPageDefaults.json")!;
        var defaults = await JsonSerializer.DeserializeAsync<List<SeedPage>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var categories = await db.PortfolioCategories.ToDictionaryAsync(x => x.Key, x => x.Id);
        foreach (var item in defaults!)
        {
            item.PortfolioCategoryId = item.Category != null && categories.TryGetValue(item.Category, out var id) ? id : null;
            db.PhotographyPages.Add(new PhotographyPage {
                Slug = item.Slug, Title = item.Title, TitleEn = item.TitleEn,
                Description = item.Description, DescriptionEn = item.DescriptionEn,
                Body = item.Body, BodyEn = item.BodyEn, Preparation = item.Preparation,
                PreparationEn = item.PreparationEn, PortfolioCategoryId = item.PortfolioCategoryId,
                DisplayOrder = item.DisplayOrder, IsActive = item.IsActive
            });
        }
        await db.SaveChangesAsync();
    }
    private sealed class SeedPage : PhotographyPage { public string? Category { get; set; } }
}
