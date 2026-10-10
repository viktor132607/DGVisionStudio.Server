using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DGVisionStudio.Tests.Coverage;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProgramStartupCollection
{
    public const string Name = "Program startup environment";
}

[Collection(ProgramStartupCollection.Name)]
public sealed class ProgramStartupIntegrationTests
{
    private static readonly IReadOnlyDictionary<string, string> TestEnvironment =
        new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["DOTNET_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings__DefaultConnection"] =
                "Host=test-db.internal;Port=5432;Database=dgvision_test;Username=test;Password=test",
            ["Frontend__Url"] = "http://localhost:5173",
            ["Storage__Provider"] = "FileSystem",
            ["Upload__MaxFileSizeBytes"] = "1048576",
            ["Upload__MaxFilesPerRequest"] = "10"
        };

    [Fact]
    public async Task Api_StartsWithFullPipeline_AndServesHealthReadinessAndAuthorization()
    {
        var originalValues = TestEnvironment.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable);

        try
        {
            foreach (var setting in TestEnvironment)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);

            await using var factory = new TestApiFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var health = await client.GetAsync("/api/health");
            var readiness = await client.GetAsync("/api/health/ready");
            var protectedEndpoint = await client.GetAsync("/api/admin/users?page=1&pageSize=10");
            var publicServices = await client.GetAsync("/api/photography-pages");
            var adminServices = await client.GetAsync("/api/admin/photography-pages");
            publicServices.StatusCode.Should().Be(HttpStatusCode.OK);
            adminServices.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            health.StatusCode.Should().Be(HttpStatusCode.OK);
            readiness.StatusCode.Should().Be(HttpStatusCode.OK);
            protectedEndpoint.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            health.Headers.TryGetValues("X-Content-Type-Options", out var values).Should().BeTrue();
            values.Should().Contain("nosniff");
        }
        finally
        {
            foreach (var setting in originalValues)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_DeletePhoto_RemovesPhotoAndUpdatesCover(bool useSqlite)
    {
        var originalValues = TestEnvironment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var setting in TestEnvironment)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);

            using var connection = useSqlite ? new SqliteConnection("Data Source=:memory:") : null;
            if (connection != null)
                await connection.OpenAsync();
            await using var factory = new TestApiFactory(connection);
            using var client = factory.CreateClient();
            int albumId, photoId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
                var category = new PortfolioCategory { Key = "delete-test", Name = "Test", NameEn = "Test" };
                var album = new PortfolioAlbum { PortfolioCategory = category, Title = "Delete test", Slug = "delete-test", CoverImageUrl = "/first.jpg" };
                var first = new PortfolioImage { PortfolioAlbum = album, ImageUrl = "/first.jpg", IsCover = true, IsPublished = true };
                var second = new PortfolioImage { PortfolioAlbum = album, ImageUrl = "/second.jpg", IsPublished = true };
                db.AddRange(category, album, first, second);
                await db.SaveChangesAsync();
                albumId = album.Id;
                photoId = first.Id;
            }
            client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
            client.DefaultRequestHeaders.Add("Cookie", "DGVisionStudio.Csrf=dGVzdA==");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "dGVzdA==");
            var response = await client.DeleteAsync($"/api/admin/client-galleries/{albumId}/photos/{photoId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            using var verificationScope = factory.Services.CreateScope();
            var stored = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await stored.PortfolioImages.AnyAsync(x => x.Id == photoId)).Should().BeFalse();
            var deleted = await stored.PortfolioImages.IgnoreQueryFilters().SingleAsync(x => x.Id == photoId);
            deleted.IsDeleted.Should().BeTrue();
            deleted.DeletedAtUtc.Should().NotBeNull();
            (await stored.PortfolioAlbums.FindAsync(albumId))!.CoverImageUrl.Should().Be("/second.jpg");
        }
        finally
        {
            foreach (var setting in originalValues)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    private sealed class TestAdminAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Admin"))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, "test-admin"),
                new Claim(ClaimTypes.Role, "Admin")
            }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class TestApiFactory(SqliteConnection? connection = null) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Background cleanup must not share the test's SQLite connection/transaction.
                foreach (var descriptor in services.Where(x =>
                    x.ImplementationType == typeof(DGVisionStudio.Infrastructure.Services.ExpiredGalleryCleanupService) ||
                    x.ImplementationType == typeof(DGVisionStudio.Infrastructure.Services.CalendarReminderEmailService)).ToList())
                    services.Remove(descriptor);
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                var databaseName = $"program-startup-{Guid.NewGuid():N}";
                services.AddDbContext<AppDbContext>(options =>
                {
                    if (connection != null)
                        options.UseSqlite(connection);
                    else
                        options.UseInMemoryDatabase(databaseName)
                            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
                services.AddAuthentication("TestAdmin")
                    .AddScheme<AuthenticationSchemeOptions, TestAdminAuthenticationHandler>("TestAdmin", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "TestAdmin";
                    options.DefaultChallengeScheme = "TestAdmin";
                    options.DefaultForbidScheme = "TestAdmin";
                });
            });
        }
    }
}
