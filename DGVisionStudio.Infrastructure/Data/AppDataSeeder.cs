using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

// Restored full repository portfolio seed. The typo "porfolio" is intentional: it matches
// the committed client asset directory public/images/porfolio.
public static class AppDataSeeder
{
    private const string P = "/images/porfolio/";

    private static readonly string[] EventBulgarePaths =
    {
        P + "events/bulgare/1.jpg",
        P + "events/bulgare/13.jpg",
    };

    private static readonly string[] GraduateAzraPaths =
    {
        P + "балове/Бал Азра/639766578_122099975367277251_3978753087381724830_n.jpg",
        P + "балове/Бал Азра/640072369_122099975331277251_8854217072496019133_n.jpg",
        P + "балове/Бал Азра/640365873_122099976543277251_3510489861658993091_n.jpg",
        P + "балове/Бал Азра/640973347_122099975325277251_9203183424506999673_n.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _4.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _5.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _8.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _17.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _18.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _22.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _26.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _32.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _39.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _42.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _43.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _54.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _60.jpg",
        P + "балове/Бал Азра/ГОТОВИ втори вариант _64.jpg",
    };

    private static readonly string[] GraduateVeronicaPaths =
    {
        P + "балове/Бал Вероника/DSC_1006.jpg",
        P + "балове/Бал Вероника/DSC_1015.jpg",
        P + "балове/Бал Вероника/DSC_1036.jpg",
        P + "балове/Бал Вероника/DSC_1041.jpg",
        P + "балове/Бал Вероника/DSC_1050.jpg",
        P + "балове/Бал Вероника/DSC_1060.jpg",
        P + "балове/Бал Вероника/DSC_1236.jpg",
        P + "балове/Бал Вероника/DSC_1283.jpg",
        P + "балове/Бал Вероника/DSC_1335.jpg",
        P + "балове/Бал Вероника/DSC_1374.jpg",
    };

    private static readonly string[] GraduateZaraPaths =
    {
        P + "балове/Бал Зара/DSC_0010.jpg",
        P + "балове/Бал Зара/DSC_0023.jpg",
        P + "балове/Бал Зара/DSC_0024.jpg",
        P + "балове/Бал Зара/DSC_0238.jpg",
        P + "балове/Бал Зара/DSC_0307.jpg",
        P + "балове/Бал Зара/DSC_0468.jpg",
        P + "балове/Бал Зара/DSC_9778.jpg",
        P + "балове/Бал Зара/DSC_9783.jpg",
        P + "балове/Бал Зара/DSC_9842.jpg",
        P + "балове/Бал Зара/DSC_9893.jpg",
    };

    private static readonly string[] GraduateYanaPaths =
    {
        P + "балове/Бал Яна/DSC_0606-3.jpg",
        P + "балове/Бал Яна/DSC_0607-4.jpg",
        P + "балове/Бал Яна/DSC_0632-9.jpg",
        P + "балове/Бал Яна/DSC_0676-17.jpg",
        P + "балове/Бал Яна/DSC_0696-21.jpg",
        P + "балове/Бал Яна/DSC_0700-23.jpg",
        P + "балове/Бал Яна/DSC_0725-26.jpg",
        P + "балове/Бал Яна/DSC_0777-38.jpg",
        P + "балове/Бал Яна/DSC_0837-48.jpg",
        P + "балове/Бал Яна/DSC_0860-52.jpg",
        P + "балове/Бал Яна/DSC_0910-64.jpg",
        P + "балове/Бал Яна/DSC_0916-67.jpg",
        P + "балове/Бал Яна/DSC_0964-77.jpg",
        P + "балове/Бал Яна/DSC_0971-79.jpg",
        P + "балове/Бал Яна/DSC_0981-80.jpg",
        P + "балове/Бал Яна/DSC_0987-81.jpg",
        P + "балове/Бал Яна/DSC_0993-82.jpg",
        P + "балове/Бал Яна/DSC_0997-83.jpg",
        P + "балове/Бал Яна/DSC_1000-84.jpg",
        P + "балове/Бал Яна/DSC_1003-85.jpg",
    };

    private static readonly string[] WinterPortraitPaths =
    {
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2362.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2395.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2399.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2406.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2429.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2476.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2534.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2573.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A2594.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4022.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4028.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4139.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4164.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4174.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4214.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4243.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4253.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4301.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4356.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4359.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4440.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4446.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4463.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4567.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4571.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4609.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4618.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4644.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/2U2A4663.jpg",
        P + "ПОРТРЕТ/зимна фотосесия ПОРТРЕТ/641416539_122101709805277251_8677250284073032946_n.jpg",
    };

    private static readonly string[] SpringPortraitPaths =
    {
        P + "ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6320.jpg",
        P + "ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6355.jpg",
        P + "ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6399.jpg",
        P + "ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6404.jpg",
        P + "ПОРТРЕТ/ПРОЛЕТ ПОРТРЕТ/2U2A6442.jpg",
    };

    private static readonly string[] TheodoraPortraitPaths =
    {
        P + "ПОРТРЕТ/ТЕОДОРА портрет/2U2A9140.jpg",
        P + "ПОРТРЕТ/ТЕОДОРА портрет/2U2A9146.jpg",
        P + "ПОРТРЕТ/ТЕОДОРА портрет/2U2A9150.jpg",
        P + "ПОРТРЕТ/ТЕОДОРА портрет/2U2A9183.jpg",
        P + "ПОРТРЕТ/ТЕОДОРА портрет/2U2A9187.jpg",
        P + "ПОРТРЕТ/ТЕОДОРА портрет/2U2A9206.jpg",
    };

    private static readonly string[] Baptism1Paths =
    {
        P + "кръщенета/Кръщене 1/2U2A1842.jpg",
        P + "кръщенета/Кръщене 1/2U2A1866.jpg",
        P + "кръщенета/Кръщене 1/2U2A1882.jpg",
        P + "кръщенета/Кръщене 1/2U2A1897.jpg",
        P + "кръщенета/Кръщене 1/2U2A1917.jpg",
        P + "кръщенета/Кръщене 1/2U2A1924.jpg",
        P + "кръщенета/Кръщене 1/2U2A1928.jpg",
        P + "кръщенета/Кръщене 1/2U2A1940.jpg",
        P + "кръщенета/Кръщене 1/2U2A1958.jpg",
        P + "кръщенета/Кръщене 1/2U2A1980.jpg",
        P + "кръщенета/Кръщене 1/2U2A1984.jpg",
        P + "кръщенета/Кръщене 1/2U2A1999.jpg",
        P + "кръщенета/Кръщене 1/2U2A2106.jpg",
        P + "кръщенета/Кръщене 1/2U2A2111.jpg",
        P + "кръщенета/Кръщене 1/2U2A2116.jpg",
        P + "кръщенета/Кръщене 1/2U2A2128.jpg",
        P + "кръщенета/Кръщене 1/2U2A2134.jpg",
        P + "кръщенета/Кръщене 1/2U2A2141.jpg",
        P + "кръщенета/Кръщене 1/2U2A2152.jpg",
        P + "кръщенета/Кръщене 1/2U2A2186.jpg",
        P + "кръщенета/Кръщене 1/2U2A2198.jpg",
        P + "кръщенета/Кръщене 1/2U2A2410.jpg",
        P + "кръщенета/Кръщене 1/2U2A2419.jpg",
    };

    private static readonly string[] Baptism2Paths =
    {
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A8969.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A8978.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A8999.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9018.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9040.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9089.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9102.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9202.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9210.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9220.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9253.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9256.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9287.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9344.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9495.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9506.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9507.jpg",
        P + "кръщенета/КРЪЩЕНЕ 2/2U2A9551.jpg",
    };

    private static readonly string[] Wedding1Paths =
    {
        P + "СВАТБИ/СВАТБА 1/652218256_122105935065277251_6910562629463362805_n.jpg",
        P + "СВАТБИ/СВАТБА 1/652324174_122105935173277251_928166735313199811_n.jpg",
        P + "СВАТБИ/СВАТБА 1/652662412_122105935227277251_5451408023515913260_n.jpg",
        P + "СВАТБИ/СВАТБА 1/652929756_122105935101277251_5371404381692777414_n.jpg",
        P + "СВАТБИ/СВАТБА 1/653345408_122105935437277251_4254495819981584913_n.jpg",
        P + "СВАТБИ/СВАТБА 1/653759335_122105935497277251_2075686935719957022_n.jpg",
        P + "СВАТБИ/СВАТБА 1/654688641_122105935335277251_7457760121515071198_n.jpg",
        P + "СВАТБИ/СВАТБА 1/654796604_122105935269277251_6083788571253739878_n.jpg",
        P + "СВАТБИ/СВАТБА 1/654846086_122105935389277251_2252568087198579211_n.jpg",
        P + "СВАТБИ/СВАТБА 1/654965058_122105935551277251_1647099086538045574_n.jpg",
    };

    private static readonly string[] Wedding2Paths =
    {
        P + "СВАТБИ/СВАТБА 2/2U2A8184-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8185-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8186-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8187-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8190-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8192-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8197.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8200-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8203-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8210-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8214-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8217-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8221-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8231-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8233-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8239-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8243-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8249-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8254-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8269-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8274-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8285-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8328-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8335-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8351-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8352-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8353-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8412-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8419-Enhanced-NR.jpg",
        P + "СВАТБИ/СВАТБА 2/2U2A8424-Enhanced-NR.jpg",
    };

    private static readonly string[] Wedding3Paths =
    {
        P + "СВАТБИ/СВАТБА 3/2U2A1549.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1570.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1646.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1648.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1654.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1656.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1658.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1661.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1677.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1688.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1693.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1707.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1723.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1779.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1788.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1895.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1954.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1958.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1968.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1975.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A1976.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2042.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2047.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2049.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2060.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2077.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2095.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2096.jpg",
        P + "СВАТБИ/СВАТБА 3/2U2A2104.jpg",
    };

    private static readonly string[] LandscapePaths =
    {
        P + "ПЕЙЗАЖИ/650235666_122104710225277251_7176854112806431771_n.jpg",
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
                if (!createResult.Succeeded) continue;
            }
            else
            {
                user.EmailConfirmed = true;
                user.IsBlocked = false;
                user.UserName = email;
                user.Email = email;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded) continue;
            }

            if (!await userManager.IsInRoleAsync(user, "Admin")) await userManager.AddToRoleAsync(user, "Admin");
            if (!await userManager.IsInRoleAsync(user, "User")) await userManager.AddToRoleAsync(user, "User");
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

        var existingCategories = await db.PortfolioCategories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync();
        foreach (var seed in categorySeeds)
        {
            var category = existingCategories.FirstOrDefault(x => x.Key == seed.Key);
            if (category == null)
            {
                category = new PortfolioCategory { Key = seed.Key };
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

        var categoriesByKey = await db.PortfolioCategories.AsNoTracking().ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var albumSeeds = new List<AlbumSeed>
        {
            new("portrait-winter", "portrait", "Зимна фотосесия", "Winter Portrait Session", 1, WinterPortraitPaths),
            new("portrait-spring", "portrait", "Пролетен портрет", "Spring Portrait", 2, SpringPortraitPaths),
            new("portrait-theodora", "portrait", "Теодора портрет", "Theodora Portrait", 3, TheodoraPortraitPaths),
            new("event-bulgare", "event", "Bulgare", "Bulgare", 1, EventBulgarePaths),
            new("graduate-azra", "graduate", "Бал Азра", "Azra Prom", 1, GraduateAzraPaths),
            new("graduate-veronica", "graduate", "Бал Вероника", "Veronica Prom", 2, GraduateVeronicaPaths),
            new("graduate-zara", "graduate", "Бал Зара", "Zara Prom", 3, GraduateZaraPaths),
            new("graduate-yana", "graduate", "Бал Яна", "Yana Prom", 4, GraduateYanaPaths),
            new("baptism-1", "baptism", "Кръщене 1", "Baptism 1", 1, Baptism1Paths),
            new("baptism-2", "baptism", "Кръщене 2", "Baptism 2", 2, Baptism2Paths),
            new("wedding-1", "wedding", "Сватба 1", "Wedding 1", 1, Wedding1Paths),
            new("wedding-2", "wedding", "Сватба 2", "Wedding 2", 2, Wedding2Paths),
            new("wedding-3", "wedding", "Сватба 3", "Wedding 3", 3, Wedding3Paths),
            new("landscape-main", "landscape", "Пейзажи", "Landscape", 1, LandscapePaths)
        };

        var existingAlbums = await db.PortfolioAlbums.Include(x => x.Images).ToListAsync();
        foreach (var seed in albumSeeds)
        {
            if (!categoriesByKey.TryGetValue(seed.CategoryKey, out var category)) continue;
            var album = existingAlbums.FirstOrDefault(x => x.Slug == seed.Slug);
            if (album == null)
            {
                album = new PortfolioAlbum { Slug = seed.Slug, CreatedAtUtc = DateTime.UtcNow };
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

        var allAlbums = await db.PortfolioAlbums.Include(x => x.Images).ToListAsync();
        foreach (var seed in albumSeeds)
        {
            var album = allAlbums.FirstOrDefault(x => x.Slug == seed.Slug);
            if (album == null) continue;

            // A repository seed owns the images of these built-in albums. Remove stale rows
            // that are no longer in the canonical full path list, then add/update every photo.
            var expected = seed.Paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var stale = album.Images.Where(x => !expected.Contains(x.ImageUrl)).ToList();
            if (stale.Count > 0)
            {
                db.PortfolioImages.RemoveRange(stale);
                foreach (var item in stale) album.Images.Remove(item);
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
        if (await db.Services.AnyAsync()) return;
        db.Services.AddRange(
            new Service { Title = "Portrait Photography", ShortDescription = "Studio and outdoor portraits.", Description = "Individual, creative and professional portrait sessions for personal brand, lifestyle and social media.", DisplayOrder = 1, IsActive = true },
            new Service { Title = "Event Photography", ShortDescription = "Coverage for private and corporate events.", Description = "Professional coverage for parties, birthdays, corporate gatherings, baptisms and other events.", DisplayOrder = 2, IsActive = true },
            new Service { Title = "Wedding Photography", ShortDescription = "Complete wedding coverage.", Description = "Documentary and artistic wedding photography covering the key moments of the day.", DisplayOrder = 3, IsActive = true });
        await db.SaveChangesAsync();
    }

    private static async Task SeedTestimonialsTestData(AppDbContext db)
    {
        if (await db.Testimonials.AnyAsync()) return;
        db.Testimonials.AddRange(
            new Testimonial { ClientName = "Maria Ivanova", ClientRole = "Brand Owner", Content = "Very professional work, fast communication and excellent final result.", Rating = 5, DisplayOrder = 1, IsPublished = true },
            new Testimonial { ClientName = "Nikolay Petrov", ClientCompany = "NP Events", Content = "Strong event coverage and reliable delivery after the shoot.", Rating = 5, DisplayOrder = 2, IsPublished = true });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSiteSettings(AppDbContext db)
    {
        if (await db.SiteSettings.AnyAsync()) return;
        db.SiteSettings.AddRange(
            new SiteSetting { Key = "site.name", Value = "DG Vision Studio", Description = "Public website name." },
            new SiteSetting { Key = "site.email", Value = "dgvisionstudio@gmail.com", Description = "Primary public contact email." },
            new SiteSetting { Key = "site.phone", Value = "+359988758434", Description = "Primary public phone." },
            new SiteSetting { Key = "site.instagram", Value = "", Description = "Instagram profile URL." });
        await db.SaveChangesAsync();
    }

    private sealed record CategorySeed(string Key, string Name, string NameEn, string Description, int DisplayOrder, bool IsActive);
    private sealed record AlbumSeed(string Slug, string CategoryKey, string Title, string TitleEn, int DisplayOrder, IReadOnlyList<string> Paths);
}
