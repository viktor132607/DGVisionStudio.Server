using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260909213000_ResetDatabaseForRepositorySeed")]
    public partial class ResetDatabaseForRepositorySeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    table_record RECORD;
                BEGIN
                    FOR table_record IN
                        SELECT tablename
                        FROM pg_tables
                        WHERE schemaname = 'public'
                          AND tablename <> '__EFMigrationsHistory'
                    LOOP
                        EXECUTE format(
                            'TRUNCATE TABLE %I.%I RESTART IDENTITY CASCADE',
                            'public',
                            table_record.tablename
                        );
                    END LOOP;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data reset is intentionally irreversible.
        }
    }
}
