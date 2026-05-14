using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class AppDataSeeder
{
	public static async Task SeedAsync(IServiceProvider services)
	{
		using var scope = services.CreateScope();

		var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
		var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		await SeedRoles(roleManager);
		await SeedAdmins(userManager, configuration);
		await SeedPortfolioTestData(db);
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

	private static async Task SeedPortfolioTestData(AppDbContext db)
	{
		if (await db.PortfolioCategories.AnyAsync())
			return;

		var portrait = new PortfolioCategory
		{
			Key = "portrait",
			Name = "Портрети",
			NameEn = "Portraits",
			Description = "Тестова категория за портрети.",
			DisplayOrder = 1,
			IsActive = true
		};

		var wedding = new PortfolioCategory
		{
			Key = "wedding",
			Name = "Сватби",
			NameEn = "Weddings",
			Description = "Тестова категория за сватби.",
			DisplayOrder = 2,
			IsActive = true
		};

		var eventCategory = new PortfolioCategory
		{
			Key = "event",
			Name = "Събития",
			NameEn = "Events",
			Description = "Тестова категория за събития.",
			DisplayOrder = 3,
			IsActive = true
		};

		db.PortfolioCategories.AddRange(portrait, wedding, eventCategory);
		await db.SaveChangesAsync();

		var albums = new List<PortfolioAlbum>
		{
			new()
			{
				PortfolioCategoryId = portrait.Id,
				Slug = "test-portrait-session",
				Title = "Тестова портретна фотосесия",
				TitleEn = "Test Portrait Session",
				Description = "Тестов албум за портрети.",
				CoverImageUrl = "/images/og-cover.jpg",
				DisplayOrder = 1,
				IsPublished = true,
				AllowClientAccess = true,
				IsUserUploaded = false,
				CreatedAtUtc = DateTime.UtcNow
			},
			new()
			{
				PortfolioCategoryId = wedding.Id,
				Slug = "test-wedding-session",
				Title = "Тестова сватба",
				TitleEn = "Test Wedding Session",
				Description = "Тестов албум за сватби.",
				CoverImageUrl = "/images/og-cover.jpg",
				DisplayOrder = 1,
				IsPublished = true,
				AllowClientAccess = true,
				IsUserUploaded = false,
				CreatedAtUtc = DateTime.UtcNow
			},
			new()
			{
				PortfolioCategoryId = eventCategory.Id,
				Slug = "test-event-session",
				Title = "Тестово събитие",
				TitleEn = "Test Event Session",
				Description = "Тестов албум за събития.",
				CoverImageUrl = "/images/og-cover.jpg",
				DisplayOrder = 1,
				IsPublished = true,
				AllowClientAccess = true,
				IsUserUploaded = false,
				CreatedAtUtc = DateTime.UtcNow
			}
		};

		db.PortfolioAlbums.AddRange(albums);
		await db.SaveChangesAsync();

		foreach (var album in albums)
		{
			db.PortfolioImages.Add(new PortfolioImage
			{
				PortfolioAlbumId = album.Id,
				ImageUrl = "/images/og-cover.jpg",
				ThumbnailUrl = "/images/og-cover.jpg",
				AltText = album.Title,
				Caption = album.Description,
				DisplayOrder = 1,
				IsCover = true,
				IsPublished = true,
				CreatedAtUtc = DateTime.UtcNow
			});
		}

		await db.SaveChangesAsync();
	}

	private static async Task SeedServicesTestData(AppDbContext db)
	{
		if (await db.Services.AnyAsync())
			return;

		db.Services.AddRange(
			new Service
			{
				Title = "Portrait Photography",
				ShortDescription = "Studio and outdoor portraits.",
				Description = "Test service for portrait photography.",
				DisplayOrder = 1,
				IsActive = true
			},
			new Service
			{
				Title = "Event Photography",
				ShortDescription = "Private and corporate events.",
				Description = "Test service for event photography.",
				DisplayOrder = 2,
				IsActive = true
			},
			new Service
			{
				Title = "Wedding Photography",
				ShortDescription = "Wedding coverage.",
				Description = "Test service for wedding photography.",
				DisplayOrder = 3,
				IsActive = true
			}
		);

		await db.SaveChangesAsync();
	}

	private static async Task SeedTestimonialsTestData(AppDbContext db)
	{
		if (await db.Testimonials.AnyAsync())
			return;

		db.Testimonials.AddRange(
			new Testimonial
			{
				ClientName = "Test Client 1",
				ClientRole = "Client",
				Content = "Great test testimonial.",
				Rating = 5,
				DisplayOrder = 1,
				IsPublished = true
			},
			new Testimonial
			{
				ClientName = "Test Client 2",
				ClientCompany = "Test Company",
				Content = "Second test testimonial.",
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
			return;

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
}