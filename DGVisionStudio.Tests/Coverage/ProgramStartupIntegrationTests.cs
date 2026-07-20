using System.Net;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DGVisionStudio.Tests.Coverage;

public sealed class ProgramStartupIntegrationTests
{
    [Fact]
    public async Task Api_StartsWithFullPipeline_AndServesHealthReadinessAndAuthorization()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var health = await client.GetAsync("/api/health");
        var readiness = await client.GetAsync("/api/health/ready");
        var protectedEndpoint = await client.GetAsync("/api/admin/users?page=1&pageSize=10");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        readiness.StatusCode.Should().Be(HttpStatusCode.OK);
        protectedEndpoint.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        health.Headers.TryGetValues("X-Content-Type-Options", out var values).Should().BeTrue();
        values.Should().Contain("nosniff");
    }

    private sealed class TestApiFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=test-db.internal;Port=5432;Database=dgvision_test;Username=test;Password=test",
                    ["Frontend:Url"] = "http://localhost:5173",
                    ["Storage:Provider"] = "FileSystem",
                    ["Upload:MaxFileSizeBytes"] = "1048576",
                    ["Upload:MaxFilesPerRequest"] = "10"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase($"program-startup-{Guid.NewGuid():N}"));
            });
        }
    }
}
