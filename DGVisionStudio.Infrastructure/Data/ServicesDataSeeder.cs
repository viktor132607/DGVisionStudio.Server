using DGVisionStudio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class ServicesDataSeeder
{
    private static readonly ServiceSeed[] DefaultServices =
    {
        new(
            "Портретна фотография",
            "Индивидуални, артистични и професионални портрети с изчистена визия и силно присъствие.",
            "/images/portfolio/ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/641416539_122101709805277251_8677250284073032946_n.jpg",
            1),
        new(
            "Абитуриентска фотография",
            "Елегантни и запомнящи се кадри за абитуриенти с изразен стил и настроение.",
            "/images/portfolio/балове/Бал Азра/639766578_122099975367277251_3978753087381724830_n.jpg",
            2),
        new(
            "Заснемане на кръщене",
            "Дискретно и емоционално заснемане на важни семейни и ритуални моменти.",
            "/images/portfolio/кръщенета/Кръщене 1/2U2A2111.jpg",
            3),
        new(
            "Сватбена фотография",
            "Емоционални и стилни кадри, които запазват атмосферата, хората и най-силните моменти.",
            "/images/portfolio/СВАТБИ/СВАТБА 3/2U2A1723.jpg",
            4),
        new(
            "Пейзажна фотография",
            "Силни визуални кадри от природни и градски пространства с атмосфера и дълбочина.",
            "/images/portfolio/ПЕЙЗАЖИ/650235666_122104710225277251_7176854112806431771_n.jpg",
            5),
        new(
            "Заснемане на събития",
            "Отразяване на различни събития, събирания и поводи с фокус върху атмосферата и ключовите моменти.",
            "/images/portfolio/events/bulgare/1.jpg",
            6)
    };

    private static readonly string[] LegacyDemoServiceTitles =
    {
        "Portrait Photography",
        "Event Photography",
        "Wedding Photography"
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db);
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var existing = await db.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (existing.Count > 0 && !IsOnlyLegacyDemoData(existing))
        {
            return;
        }

        if (existing.Count > 0)
        {
            db.Services.RemoveRange(existing);
            await db.SaveChangesAsync();
        }

        db.Services.AddRange(DefaultServices.Select(seed => new Service
        {
            Title = seed.Title,
            ShortDescription = seed.Description,
            Description = seed.Description,
            CoverImageUrl = seed.CoverImageUrl,
            DisplayOrder = seed.DisplayOrder,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        }));

        await db.SaveChangesAsync();
    }

    private static bool IsOnlyLegacyDemoData(IEnumerable<Service> services)
    {
        var titles = services.Select(x => x.Title).ToList();
        return titles.Count <= LegacyDemoServiceTitles.Length &&
               titles.All(title => LegacyDemoServiceTitles.Contains(title, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record ServiceSeed(
        string Title,
        string Description,
        string CoverImageUrl,
        int DisplayOrder
    );
}
