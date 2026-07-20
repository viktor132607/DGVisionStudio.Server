using System.Reflection;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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

        await InvokeSeederAsync("SeedPortfolio", fixture.Context);
        await InvokeSeederAsync("SeedServicesTestData", fixture.Context);
        await InvokeSeederAsync("SeedTestimonialsTestData", fixture.Context);
        await InvokeSeederAsync("SeedSiteSettings", fixture.Context);

        (await fixture.Context.PortfolioCategories.CountAsync()).Should().Be(13);
        (await fixture.Context.PortfolioAlbums.CountAsync()).Should().Be(14);
        (await fixture.Context.PortfolioImages.CountAsync()).Should().BeGreaterThan(100);
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
