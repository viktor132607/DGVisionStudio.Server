using System.Net;
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

    private sealed class TestApiFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
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
