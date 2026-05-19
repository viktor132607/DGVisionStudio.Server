using System.Text.RegularExpressions;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryNamingService
{
	private readonly AppDbContext _dbContext;

	public ClientGalleryNamingService(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<int> EnsureClientAlbumsCategoryAsync()
	{
		var existing = await _dbContext.PortfolioCategories
			.FirstOrDefaultAsync(x => x.Key == "client-galleries" && !x.IsDeleted);

		if (existing != null) return existing.Id;

		var maxOrder = await _dbContext.PortfolioCategories
			.Where(x => !x.IsDeleted)
			.Select(x => (int?)x.DisplayOrder)
			.MaxAsync() ?? 0;

		var category = new PortfolioCategory
		{
			Key = "client-galleries",
			Name = "Клиентски албуми",
			NameEn = "Client Galleries",
			Description = "Albums created from the client gallery admin.",
			DisplayOrder = maxOrder + 1,
			IsActive = false,
			IsDeleted = false,
			DeletedAtUtc = null
		};

		_dbContext.PortfolioCategories.Add(category);
		await _dbContext.SaveChangesAsync();
		return category.Id;
	}

	public async Task<string> BuildUniqueSlugAsync(string title, int? currentAlbumId = null)
	{
		var baseSlug = Slugify(title);
		var slug = baseSlug;
		var index = 2;

		while (await _dbContext.PortfolioAlbums.AnyAsync(x => x.Slug == slug && x.Id != currentAlbumId && !x.IsDeleted))
		{
			slug = $"{baseSlug}-{index}";
			index++;
		}

		return slug;
	}

	public string Slugify(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("N");

		var slug = value.Trim().ToLowerInvariant();
		slug = Regex.Replace(slug, @"[^a-z0-9а-я]+", "-");
		slug = Regex.Replace(slug, @"-+", "-").Trim('-');

		return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
	}
}
