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
            "/images/porfolio/ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2362.jpg",
            1),
        new(
            "Абитуриентска фотография",
            "Елегантни и запомнящи се кадри за абитуриенти с изразен стил и настроение.",
            "/images/porfolio/балове/Бал Азра/640973347_122099975325277251_9203183424506999673_n.jpg",
            2),
        new(
            "Заснемане на кръщене",
            "Дискретно и емоционално заснемане на важни семейни и ритуални моменти.",
            "/images/porfolio/кръщенета/Кръщене 1/2U2A2111.jpg",
            3),
        new(
            "Сватбена фотография",
            "Емоционални и стилни кадри, които запазват атмосферата, хората и най-силните моменти.",
            "/images/porfolio/СВАТБИ/СВАТБА 3/2U2A1723.jpg",
            4),
        new(
            "Пейзажна фотография",
            "Силни визуални кадри от природни и градски пространства с атмосфера и дълбочина.",
            "/images/porfolio/ПЕЙЗАЖИ/650235666_122104710225277251_7176854112806431771_n.jpg",
            5),
        new(
            "Заснемане на събития",
            "Отразяване на различни събития, събирания и поводи с фокус върху атмосферата и ключовите моменти.",
            "/images/porfolio/events/bulgare/1.jpg",
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
