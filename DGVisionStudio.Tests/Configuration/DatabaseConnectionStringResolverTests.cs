using DGVisionStudio.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Tests.Configuration;

public sealed class DatabaseConnectionStringResolverTests
{
    [Fact]
    public void NormalizeAndValidate_ReturnsExistingNpgsqlConnectionString_ForDevelopment()
    {
        var environment = new TestHostEnvironment(Environments.Development);

        var result = DatabaseConnectionStringResolver.NormalizeAndValidate(
            "Host=localhost;Port=5432;Database=dgvisionstudio;Username=postgres;Password=postgres;",
            "ConnectionStrings:DefaultConnection",
            environment);

        result.Should().Contain("Host=localhost");
        result.Should().Contain("Database=dgvisionstudio");
        result.Should().Contain("Username=postgres");
    }

    [Fact]
    public void NormalizeAndValidate_ConvertsPostgresUrl_ToNpgsqlConnectionString()
    {
        var environment = new TestHostEnvironment(Environments.Production);

        var result = DatabaseConnectionStringResolver.NormalizeAndValidate(
            "postgresql://dgvision:secret@dpg-example:5432/dgvisiondb?sslmode=require",
            "DATABASE_URL",
            environment);

        result.Should().Contain("Host=dpg-example");
        result.Should().Contain("Database=dgvisiondb");
        result.Should().Contain("Username=dgvision");
        result.Should().Contain("Password=secret");
        result.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void NormalizeAndValidate_TrimsBalancedQuotes()
    {
        var environment = new TestHostEnvironment(Environments.Development);

        var result = DatabaseConnectionStringResolver.NormalizeAndValidate(
            "\"Host=localhost;Database=dgvisionstudio;Username=postgres;Password=postgres;\"",
            "ConnectionStrings:DefaultConnection",
            environment);

        result.Should().Contain("Host=localhost");
        result.Should().Contain("Database=dgvisionstudio");
    }

    [Fact]
    public void NormalizeAndValidate_ThrowsForMalformedConnectionString()
    {
        var environment = new TestHostEnvironment(Environments.Production);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnectionStringResolver.NormalizeAndValidate(
                "not-a-connection-string",
                "ConnectionStrings:DefaultConnection",
                environment));

        exception.Message.Should().Contain("not a valid Npgsql connection string or postgres URL");
    }

    [Fact]
    public void NormalizeAndValidate_ThrowsForLoopbackHostOutsideDevelopment()
    {
        var environment = new TestHostEnvironment(Environments.Production);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnectionStringResolver.NormalizeAndValidate(
                "Host=localhost;Port=5432;Database=dgvisionstudio;Username=postgres;Password=postgres;",
                "ConnectionStrings:DefaultConnection",
                environment));

        exception.Message.Should().Contain("local development placeholder");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
