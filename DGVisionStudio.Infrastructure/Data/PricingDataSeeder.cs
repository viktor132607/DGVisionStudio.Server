using DGVisionStudio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class PricingDataSeeder
{
    private static readonly PricingSeed[] DefaultPricingItems =
    {
        new("Портретна фотография", "Индивидуални, артистични и професионални портретни фотосесии.", "Fixed", "От 60 € / фотосесия", 1),
        new("Абитуриентска фотография", "Сесии за абитуриенти с акцент върху стил, присъствие и детайл.", "Fixed", "От 60 € / час", 2),
        new("Заснемане на кръщене", "Дискретно и емоционално заснемане на важни семейни моменти.", "Fixed", "От 60 € / час", 3),
        new("Сватбена фотография", "Заснемане на сватбен ден, ключови моменти и атмосфера.", "Fixed", "120 € / час + 30 € дрон", 4),
        new("Продуктова фотография", "Кадри за продукти, брандове и онлайн магазини.", "Negotiable", null, 5),
        new("Рекламна фотография", "Съдържание за кампании, социални мрежи и бранд присъствие.", "Negotiable", null, 6),
        new("Корпоративна фотография", "Професионални кадри за екипи, бизнес среда и услуги.", "Negotiable", null, 7),
        new("Семейна фотография", "Естествени и емоционални кадри за семейства и деца.", "Negotiable", null, 8),
        new("Заснемане на събития", "Заснемане на частни, фирмени и сценични събития.", "Fixed", "От 60 € / час", 9)
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db);
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        if (await db.PricingItems.AnyAsync()) return;

        db.PricingItems.AddRange(DefaultPricingItems.Select(item => new PricingItem
        {
            Title = item.Title,
            Description = item.Description,
            PricingMode = item.PricingMode,
            PriceText = item.PriceText,
            DisplayOrder = item.DisplayOrder,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        }));

        await db.SaveChangesAsync();
    }

    private sealed record PricingSeed(
        string Title,
        string Description,
        string PricingMode,
        string? PriceText,
        int DisplayOrder
    );
}
