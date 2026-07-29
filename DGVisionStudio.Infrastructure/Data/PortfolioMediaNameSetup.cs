using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class PortfolioMediaNameSetup
{
	public static async Task EnsureAsync(IServiceProvider services)
	{
		using var scope = services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		await db.Database.ExecuteSqlRawAsync("""
			ALTER TABLE "PortfolioImages"
			ADD COLUMN IF NOT EXISTS "Name" character varying(250) NULL;

			ALTER TABLE "PortfolioAlbums"
			ADD COLUMN IF NOT EXISTS "PublishAtUtc" timestamp with time zone NULL;

			CREATE INDEX IF NOT EXISTS "IX_PortfolioAlbums_PublishAtUtc"
			ON "PortfolioAlbums" ("PublishAtUtc");
			""");

		await db.Database.ExecuteSqlRawAsync("""
			UPDATE "PortfolioImages"
			SET "Name" = "AltText",
				"AltText" = NULL
			WHERE ("Name" IS NULL OR "Name" = '')
				AND "AltText" IS NOT NULL
				AND "AltText" <> ''
				AND ("Caption" IS NULL OR "Caption" = '');
			""");
	}
}
