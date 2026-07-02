using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class PortfolioMediaNameSetup
{
	public static async Task EnsureAsync(IServiceProvider services)
	{
		using var scope = services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		const string sql = "ALTER TABLE \"PortfolioImages\" ADD COLUMN IF NOT EXISTS \"Name\" character varying(250) NULL;";
		await db.Database.ExecuteSqlRawAsync(sql);
	}
}
