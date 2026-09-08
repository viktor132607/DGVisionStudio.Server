using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class AppDataSeeder
{
    private static readonly string[] EventBulgarePaths =
    {
        "/images/porfolio/events/bulgare/1.jpg",
        "/images/porfolio/events/bulgare/13.jpg",
        "/images/porfolio/events/bulgare/2.jpg",
        "/images/porfolio/events/bulgare/3.jpg"
    };

    private static readonly string[] GraduateAzraPaths =
    {
        "/images/porfolio/балове/Бал Азра/640973347_122099975325277251_9203183424506999673_n.jpg"
    };

    private static readonly string[] WinterPortraitPaths =
    {
        "/images/porfolio/ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2362.jpg"
    };

    private static readonly string[] SpringPortraitPaths =
    {
        "/images/porfolio/ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6320.jpg",
        "/images/porfolio/ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6355.jpg",
        "/images/porfolio/ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6399.jpg",
        "/images/porfolio/ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6404.jpg",
        "/images/porfolio/ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6442.jpg"
    };

    private static readonly string[] Baptism1Paths =
    {
        "/images/porfolio/кръщенета/Кръщене 1/2U2A2111.jpg"
    };

    private static readonly string[] Wedding3Paths =
    {
        "/images/porfolio/СВАТБИ/СВАТБА 3/2U2A1723.jpg"
    };

    private static readonly string[] LandscapePaths =
    {
        "/images/porfolio/ПЕЙЗАЖИ/650235666_122104710225277251_7176854112806431771_n.jpg"
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedRoles(roleManager);
        await SeedAdmins(userManager, configuration);
        await SeedPortfolio(db);
        await SeedServicesTestData(db);
        await SeedTestimonialsTestData(db);
        await SeedSiteSettings(db);
    }

    private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedAdmins(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var adminPassword = string.IsNullOrWhiteSpace(configuration["Seed:AdminPassword"])
            ? "Admin123!"
            : configuration["Seed:AdminPassword"]!;

        var emails = new[]
        {
            configuration["Seed:PrimaryAdminEmail"] ?? "dgvisionstudio@gmail.com",
            configuration["Seed:SecondaryAdminEmail"] ?? "iliev132607@gmail.com"
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var email in emails)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    IsBlocked = false
                };

                var createResult = await userManager.CreateAsync(user, adminPassword);
                if (!createResult.Succeeded)
                {
                    continue;
                }
            }
            else
            {
                user.EmailConfirmed = true;
                user.IsBlocked = false;
                user.UserName = email;
                user.Email = email;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Admin"))
            {
                await userManager.AddToRoleAsync(user, "Admin");
            }

            if (!await userManager.IsInRoleAsync(user, "User"))
            {
                await userManager.AddToRoleAsync(user, "User");
            }
        }
    }

    private static async Task SeedPortfolio(AppDbContext db)
    {
        var categorySeeds = new[]
        {
            new CategorySeed("portrait", "Портрети", "Portraits", "Портретни фотосесии и personal branding.", 1, true),
            new CategorySeed("product", "Продукти", "Products", "Продуктова фотография.", 2, true),
            new CategorySeed("commercial", "Рекламни", "Commercial", "Рекламна фотография.", 3, true),
            new CategorySeed("corporate", "Корпоративни", "Corporate", "Корпоративна фотография.", 4, true),
            new CategorySeed("event", "Събития", "Events", "Частни и бизнес събития.", 5, true),
            new CategorySeed("graduate", "Абитуриентски", "Graduation", "Абитуриентска фотография.", 6, true),
            new CategorySeed("birthday", "Детски рождени дни", "Birthday", "Фотография за рождени дни.", 7, true),
            new CategorySeed("christmas", "Коледни", "Christmas", "Коледна фотография.", 8, true),
            new CategorySeed("baptism", "Кръщенета", "Baptism", "Фотография за кръщенета.", 9, true),
            new CategorySeed("wedding", "Сватби", "Weddings", "Сватбена фотография.", 10, true),
            new CategorySeed("family", "Семейни", "Family", "Семейна фотография.", 11, true),
            new CategorySeed("maternity", "Бременни", "Maternity", "Фотография за бременност.", 12, true),
            new CategorySeed("landscape", "Пейзажи", "Landscape", "Пейзажна фотография.", 13, true)
        };

        var existingCategories = await db.PortfolioCategories
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        foreach (var seed in categorySeeds)
        {
            var category = existingCategories.FirstOrDefault(x => x.Key == seed.Key);
            if (category == null)
            {
                category = new PortfolioCategory
                {
                    Key = seed.Key
                };

                db.PortfolioCategories.Add(category);
                existingCategories.Add(category);
            }

            category.Name = seed.Name;
            category.NameEn = seed.NameEn;
            category.Description = seed.Description;
            category.DisplayOrder = seed.DisplayOrder;
            category.IsActive = seed.IsActive;
        }

        await db.SaveChangesAsync();

        var categoriesByKey = await db.PortfolioCategories
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase);

        var albumSeeds = new List<AlbumSeed>
        {
            new("portrait-winter", "portrait", "Зимна фотосесия", "Winter Portrait Session", 1, WinterPortraitPaths),
            new("portrait-spring", "portrait", "Пролетен портрет", "Spring Portrait", 2, SpringPortraitPaths),
            new("event-bulgare", "event", "Bulgare", "Bulgare", 1, EventBulgarePaths),
            new("graduate-azra", "graduate", "Бал Азра", "Azra Prom", 1, GraduateAzraPaths),
            new("baptism-1", "baptism", "Кръщене 1", "Baptism 1", 1, Baptism1Paths),
            new("wedding-3", "wedding", "Сватба 3", "Wedding 3", 1, Wedding3Paths),
            new("landscape-main", "landscape", "Пейзажи", "Landscape", 1, LandscapePaths)
        };

        var existingAlbums = await db.PortfolioAlbums
            .Include(x => x.Images)
            .ToListAsync();

        foreach (var seed in albumSeeds)
        {
            if (!categoriesByKey.TryGetValue(seed.CategoryKey, out var category))
            {
                continue;
            }

            var album = existingAlbums.FirstOrDefault(x => x.Slug == seed.Slug);
            if (album == null)
            {
                album = new PortfolioAlbum
                {
                    Slug = seed.Slug,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.PortfolioAlbums.Add(album);
                existingAlbums.Add(album);
            }

            album.PortfolioCategoryId = category.Id;
            album.Title = seed.Title;
            album.TitleEn = seed.TitleEn;
            album.Description = seed.Title;
            album.DisplayOrder = seed.DisplayOrder;
            album.IsPublished = true;
            album.AllowClientAccess = true;
            album.IsUserUploaded = false;
            album.CoverImageUrl = seed.Paths.FirstOrDefault();
        }

        await db.SaveChangesAsync();

        var allAlbums = await db.PortfolioAlbums
            .Include(x => x.Images)
            .ToListAsync();

        foreach (var seed in albumSeeds)
        {
            var album = allAlbums.FirstOrDefault(x => x.Slug == seed.Slug);
            if (album == null)
            {
                continue;
            }

            var existingImagesByUrl = album.Images.ToDictionary(x => x.ImageUrl, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < seed.Paths.Count; i++)
            {
                var path = seed.Paths[i];

                if (!existingImagesByUrl.TryGetValue(path, out var image))
                {
                    image = new PortfolioImage
                    {
                        PortfolioAlbumId = album.Id,
                        ImageUrl = path,
                        ThumbnailUrl = path,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    db.PortfolioImages.Add(image);
                    album.Images.Add(image);
                }

                image.ImageUrl = path;
                image.ThumbnailUrl = path;
                image.AltText = $"{seed.Title} {i + 1}";
                image.Caption = null;
                image.DisplayOrder = i + 1;
                image.IsCover = i == 0;
                image.IsPublished = true;
            }

            album.CoverImageUrl = seed.Paths.FirstOrDefault();
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedServicesTestData(AppDbContext db)
    {
        if (await db.Services.AnyAsync())
        {
            return;
        }

        db.Services.AddRange(
            new Service
            {
                Title = "Portrait Photography",
                ShortDescription = "Studio and outdoor portraits.",
                Description = "Individual, creative and professional portrait sessions for personal brand, lifestyle and social media.",
                CoverImageUrl = WinterPortraitPaths[0],
                DisplayOrder = 1,
                IsActive = true
            },
            new Service
            {
                Title = "Event Photography",
                ShortDescription = "Coverage for private and corporate events.",
                Description = "Professional coverage for parties, birthdays, corporate gatherings, baptisms and other events.",
                CoverImageUrl = EventBulgarePaths[0],
                DisplayOrder = 2,
                IsActive = true
            },
            new Service
            {
                Title = "Wedding Photography",
                ShortDescription = "Complete wedding coverage.",
                Description = "Documentary and artistic wedding photography covering the key moments of the day.",
                CoverImageUrl = Wedding3Paths[0],
                DisplayOrder = 3,
                IsActive = true
            }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedTestimonialsTestData(AppDbContext db)
    {
        if (await db.Testimonials.AnyAsync())
        {
            return;
        }

        db.Testimonials.AddRange(
            new Testimonial
            {
                ClientName = "Maria Ivanova",
                ClientRole = "Brand Owner",
                Content = "Very professional work, fast communication and excellent final result.",
                Rating = 5,
                DisplayOrder = 1,
                IsPublished = true
            },
            new Testimonial
            {
                ClientName = "Nikolay Petrov",
                ClientCompany = "NP Events",
                Content = "Strong event coverage and reliable delivery after the shoot.",
                Rating = 5,
                DisplayOrder = 2,
                IsPublished = true
            }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedSiteSettings(AppDbContext db)
    {
        if (await db.SiteSettings.AnyAsync())
        {
            return;
        }

        db.SiteSettings.AddRange(
            new SiteSetting
            {
                Key = "site.name",
                Value = "DG Vision Studio",
                Description = "Public website name."
            },
            new SiteSetting
            {
                Key = "site.email",
                Value = "dgvisionstudio@gmail.com",
                Description = "Primary public contact email."
            },
            new SiteSetting
            {
                Key = "site.phone",
                Value = "+359988758434",
                Description = "Primary public phone."
            },
            new SiteSetting
            {
                Key = "site.instagram",
                Value = "",
                Description = "Instagram profile URL."
            }
        );

        await db.SaveChangesAsync();
    }

    private sealed record CategorySeed(
        string Key,
        string Name,
        string NameEn,
        string Description,
        int DisplayOrder,
        bool IsActive
    );

    private sealed record AlbumSeed(
        string Slug,
        string CategoryKey,
        string Title,
        string TitleEn,
        int DisplayOrder,
        IReadOnlyList<string> Paths
    );
}
