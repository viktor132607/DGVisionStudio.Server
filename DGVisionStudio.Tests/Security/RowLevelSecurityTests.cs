using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DGVisionStudio.Tests.Security;

public sealed class RowLevelSecurityTests
{
    [Fact]
    public async Task PortfolioAlbumsAreIsolatedByDatabasePolicy()
    {
        string? adminConnection =
            Environment.GetEnvironmentVariable("BACKUP_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(adminConnection))
        {
            return;
        }

        string suffix = Guid.NewGuid().ToString("N");
        string databaseName = "rls_test_" + suffix;
        string roleName = "rls_app_" + suffix;
        string rolePassword = "RlsTest_" + suffix + "!";

        await using NpgsqlConnection admin = new(adminConnection);
        await admin.OpenAsync();

        await ExecuteAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        await ExecuteAsync(
            admin,
            $"CREATE ROLE \"{roleName}\" LOGIN PASSWORD '{rolePassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS");

        NpgsqlConnectionStringBuilder builder =
            new(adminConnection)
            {
                Database = databaseName,
                Pooling = false
            };

        try
        {
            DbContextOptions<AppDbContext> options =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(builder.ConnectionString)
                    .Options;

            await using (AppDbContext context = new(options))
            {
                await context.Database.MigrateAsync();

                context.Users.AddRange(
                    new ApplicationUser
                    {
                        Id = "user-a",
                        UserName = "a@example.test",
                        NormalizedUserName = "A@EXAMPLE.TEST",
                        Email = "a@example.test",
                        NormalizedEmail = "A@EXAMPLE.TEST",
                        SecurityStamp = Guid.NewGuid().ToString(),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    },
                    new ApplicationUser
                    {
                        Id = "user-b",
                        UserName = "b@example.test",
                        NormalizedUserName = "B@EXAMPLE.TEST",
                        Email = "b@example.test",
                        NormalizedEmail = "B@EXAMPLE.TEST",
                        SecurityStamp = Guid.NewGuid().ToString(),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    });

                PortfolioCategory category = new()
                {
                    Key = "private-test",
                    Name = "Private",
                    NameEn = "Private",
                    IsActive = true
                };
                context.PortfolioCategories.Add(category);
                await context.SaveChangesAsync();

                context.PortfolioAlbums.AddRange(
                    new PortfolioAlbum
                    {
                        PortfolioCategoryId = category.Id,
                        Slug = "private-a",
                        Title = "Private A",
                        OwnerUserId = "user-a",
                        IsUserUploaded = true,
                        IsPublished = false
                    },
                    new PortfolioAlbum
                    {
                        PortfolioCategoryId = category.Id,
                        Slug = "private-b",
                        Title = "Private B",
                        OwnerUserId = "user-b",
                        IsUserUploaded = true,
                        IsPublished = false
                    });
                await context.SaveChangesAsync();
            }

            await using (NpgsqlConnection ownerConnection = new(builder.ConnectionString))
            {
                await ownerConnection.OpenAsync();
                await ExecuteAsync(
                    ownerConnection,
                    $"""
                    GRANT CONNECT ON DATABASE "{databaseName}" TO "{roleName}";
                    GRANT USAGE ON SCHEMA public TO "{roleName}";
                    GRANT SELECT ON "PortfolioAlbums", "UserAlbumAccesses" TO "{roleName}";
                    """);
            }

            NpgsqlConnectionStringBuilder appBuilder = new(builder.ConnectionString)
            {
                Username = roleName,
                Password = rolePassword
            };

            await using NpgsqlConnection db = new(appBuilder.ConnectionString);
            await db.OpenAsync();

            await SetApplicationContextAsync(db, "user-a", false, false);
            Assert.Equal(
                "user-a",
                await ScalarAsync(
                    db,
                    """SELECT string_agg("OwnerUserId", ',' ORDER BY "OwnerUserId") FROM "PortfolioAlbums";"""));

            await SetApplicationContextAsync(db, "user-b", false, false);
            Assert.Equal(
                "user-b",
                await ScalarAsync(
                    db,
                    """SELECT string_agg("OwnerUserId", ',' ORDER BY "OwnerUserId") FROM "PortfolioAlbums";"""));

            await SetApplicationContextAsync(db, "user-a", true, false);
            Assert.Equal(
                "2",
                await ScalarAsync(
                    db,
                    """SELECT count(*) FROM "PortfolioAlbums";"""));
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP DATABASE \"{databaseName}\" WITH (FORCE)");
            await ExecuteAsync(admin, $"DROP ROLE IF EXISTS \"{roleName}\"");
        }
    }

    private static async Task SetApplicationContextAsync(
        NpgsqlConnection connection,
        string userId,
        bool isAdmin,
        bool isSystem)
    {
        await using NpgsqlCommand command = new(
            """
            SELECT
                set_config('app.current_user_id', @user_id, false),
                set_config('app.current_is_admin', @is_admin, false),
                set_config('app.current_is_system', @is_system, false);
            """,
            connection);

        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("is_admin", isAdmin ? "true" : "false");
        command.Parameters.AddWithValue("is_system", isSystem ? "true" : "false");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql) =>
        await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();

    private static async Task<string?> ScalarAsync(
        NpgsqlConnection connection,
        string sql) =>
        (await new NpgsqlCommand(sql, connection).ExecuteScalarAsync())?.ToString();
}
