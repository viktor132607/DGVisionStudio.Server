using System.Reflection;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Tests.Coverage;

public sealed class AppDataSeederTests
{
    [Fact]
    public async Task ApplicationDataSeeders_AreIdempotentAndPopulateExpectedDefaults()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();

        await InvokeSeederAsync("SeedPortfolio", fixture.Context);
        await InvokeSeederAsync("SeedServicesTestData", fixture.Context);
        await InvokeSeederAsync("SeedTestimonialsTestData", fixture.Context);
        await InvokeSeederAsync("SeedSiteSettings", fixture.Context);

        fixture.Context.ChangeTracker.Clear();

        await InvokeSeederAsync("SeedPortfolio", fixture.Context);
        await InvokeSeederAsync("SeedServicesTestData", fixture.Context);
        await InvokeSeederAsync("SeedTestimonialsTestData", fixture.Context);
        await InvokeSeederAsync("SeedSiteSettings", fixture.Context);

        (await fixture.Context.PortfolioCategories.CountAsync()).Should().Be(13);
        (await fixture.Context.PortfolioAlbums.CountAsync()).Should().Be(14);
        (await fixture.Context.PortfolioImages.CountAsync()).Should().Be(214);
        (await fixture.Context.Services.CountAsync()).Should().Be(3);
        (await fixture.Context.Testimonials.CountAsync()).Should().Be(2);
        (await fixture.Context.SiteSettings.CountAsync()).Should().Be(4);

        var portraits = await fixture.Context.PortfolioCategories.SingleAsync(x => x.Key == "portrait");
        portraits.Name.Should().Be("Портрети");
        portraits.DisplayOrder.Should().Be(1);

        var winter = await fixture.Context.PortfolioAlbums
            .Include(x => x.Images)
            .SingleAsync(x => x.Slug == "portrait-winter");
        winter.IsPublished.Should().BeTrue();
        winter.Images.Should().NotBeEmpty();
        winter.Images.Count(x => x.IsCover).Should().Be(1);
    }

    [Fact]
    public async Task PortfolioSeeders_PreserveBulkMovesDeletesAndAdminEditsOnRestart()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var db = fixture.Context;
        await InvokeSeederAsync("SeedPortfolio", db);
        var winter = await db.PortfolioAlbums.Include(a => a.Images).SingleAsync(a => a.Slug == "portrait-winter");
        var baptism = await db.PortfolioAlbums.SingleAsync(a => a.Slug == "baptism-1");
        var target = await db.PortfolioCategories.SingleAsync(c => c.Key == "wedding");
        var portraits = await db.PortfolioCategories.SingleAsync(c => c.Key == "portrait");
        portraits.IsActive = false;
        portraits.Name = "Редактирана категория";
        winter.Title = "Редактиран албум";
        winter.Images.Add(new PortfolioImage { ImageUrl = "/uploads/custom.jpg", Name = "Custom" });
        var eventPhoto = await db.PortfolioImages.SingleAsync(p => p.ImageUrl == "/images/porfolio/events/bulgare/2.jpg");
        eventPhoto.IsDeleted = true;
        await db.SaveChangesAsync();
        var bulk = new PortfolioAlbumBulkService(db, new RecordingAuditLogService());
        var admin = new AdminRequestContext("admin", "admin@test.bg", "Admin", null, "tests", "seed-restart");
        (await bulk.ExecuteAsync([winter.Id], target.Id, admin, default)).StatusCode.Should().Be(200);
        (await bulk.ExecuteAsync([baptism.Id], null, admin, default)).StatusCode.Should().Be(200);
        db.ChangeTracker.Clear();

        await InvokeSeederAsync("SeedPortfolio", db);
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(fixture.Connection));
        await using (var provider = services.BuildServiceProvider()) await TemporaryPortfolioPathSeeder.SeedAsync(provider);
        db.ChangeTracker.Clear();

        var moved = await db.PortfolioAlbums.Include(a => a.Images).SingleAsync(a => a.Id == winter.Id);
        moved.PortfolioCategoryId.Should().Be(target.Id);
        moved.Title.Should().Be("Редактиран албум");
        moved.Images.Should().Contain(p => p.ImageUrl == "/uploads/custom.jpg");
        (await db.PortfolioAlbums.IgnoreQueryFilters().CountAsync()).Should().Be(14);
        (await db.PortfolioImages.IgnoreQueryFilters().CountAsync()).Should().Be(215);
        (await db.PortfolioAlbums.IgnoreQueryFilters().SingleAsync(a => a.Id == baptism.Id)).IsDeleted.Should().BeTrue();
        (await db.PortfolioImages.IgnoreQueryFilters().SingleAsync(p => p.Id == eventPhoto.Id)).IsDeleted.Should().BeTrue();
        var category = await db.PortfolioCategories.SingleAsync(c => c.Id == portraits.Id);
        category.IsActive.Should().BeFalse();
        category.Name.Should().Be("Редактирана категория");
    }

    private static async Task InvokeSeederAsync(string methodName, AppDbContext context)
    {
        var method = typeof(AppDataSeeder).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull($"{methodName} must remain available to the seed orchestration");
        var task = method!.Invoke(null, [context]).Should().BeAssignableTo<Task>().Subject;
        await task;
    }
}

public sealed class ProgramStartupContractTests
{
    [Fact]
    public void Program_PreservesCriticalRegistrationAndMiddlewareOrder()
    {
        var programPath = FindRepositoryFile("DGVisionStudio.Api", "Program.cs");
        var source = File.ReadAllText(programPath);

        source.Should().Contain("AddDGVisionApplicationServices");
        source.Should().Contain("ValidateOnStart");
        source.Should().Contain("AddFixedWindowLimiter(\"auth\"");
        source.Should().Contain("AddFixedWindowLimiter(\"contact\"");
        source.Should().Contain("AddFixedWindowLimiter(\"upload\"");

        AssertOrdered(
            source,
            "app.UseForwardedHeaders();",
            "app.UseMiddleware<GlobalExceptionHandlingMiddleware>();",
            "app.UseMiddleware<SecurityHeadersMiddleware>();",
            "app.UseCors(\"AllowFrontend\");",
            "app.UseRateLimiter();",
            "app.UseMiddleware<CsrfProtectionMiddleware>();",
            "app.UseAuthentication();",
            "app.UseAuthorization();",
            "app.MapControllers();");
    }

    [Fact]
    public void Program_ConfiguresUploadDirectoriesAndDevelopmentOnlyDiagnostics()
    {
        var source = File.ReadAllText(FindRepositoryFile("DGVisionStudio.Api", "Program.cs"));

        source.Should().Contain("uploads\", \"portfolio");
        source.Should().Contain("uploads\", \"client-galleries\", \"previews");
        source.Should().Contain("uploads\", \"client-galleries\", \"originals");
        source.Should().Contain("if (app.Environment.IsDevelopment())");
        source.Should().Contain("await AppDataSeeder.SeedAsync");
        source.Should().Contain("app.MapScalarApiReference");
    }

    private static void AssertOrdered(string source, params string[] snippets)
    {
        var previous = -1;
        foreach (var snippet in snippets)
        {
            var current = source.IndexOf(snippet, StringComparison.Ordinal);
            current.Should().BeGreaterThan(previous, $"'{snippet}' must appear after the previous pipeline step");
            previous = current;
        }
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine([current.FullName, .. relativeParts]);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)} from the test output directory.");
    }
}
