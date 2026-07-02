using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services;

public static class CalendarReminderSchemaSetup
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "CalendarEvents" ADD COLUMN IF NOT EXISTS "ClientEmail" character varying(150) NULL;
            ALTER TABLE "CalendarEvents" ADD COLUMN IF NOT EXISTS "ContactRequestId" uuid NULL;
            ALTER TABLE "CalendarEvents" ADD COLUMN IF NOT EXISTS "RemindersEnabled" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE "CalendarEvents" ADD COLUMN IF NOT EXISTS "Reminder24hSentAtUtc" timestamp with time zone NULL;
            ALTER TABLE "CalendarEvents" ADD COLUMN IF NOT EXISTS "Reminder2hSentAtUtc" timestamp with time zone NULL;

            CREATE INDEX IF NOT EXISTS "IX_CalendarEvents_ClientEmail" ON "CalendarEvents" ("ClientEmail");
            CREATE INDEX IF NOT EXISTS "IX_CalendarEvents_ContactRequestId" ON "CalendarEvents" ("ContactRequestId");
            CREATE INDEX IF NOT EXISTS "IX_CalendarEvents_RemindersEnabled" ON "CalendarEvents" ("RemindersEnabled");
            """);
    }
}
