using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Data;

public static class PortfolioMediaSchema
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "PortfolioImages"
            ADD COLUMN IF NOT EXISTS "MediaType" character varying(30) NOT NULL DEFAULT 'Image';

            ALTER TABLE "PortfolioImages"
            ADD COLUMN IF NOT EXISTS "ContentType" character varying(150) NULL;

            UPDATE "PortfolioImages"
            SET "MediaType" = 'Image'
            WHERE "MediaType" IS NULL OR "MediaType" = '';

            CREATE INDEX IF NOT EXISTS "IX_PortfolioImages_MediaType" ON "PortfolioImages" ("MediaType");
            """);
    }
}
