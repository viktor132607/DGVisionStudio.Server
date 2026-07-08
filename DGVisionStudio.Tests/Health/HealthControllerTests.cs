using DGVisionStudio.Api.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Tests.Health;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsHealthyStatus()
    {
        using var context = CreateContext();
        var controller = new HealthController(new TestHostEnvironment(Environments.Production), context);

        var result = controller.Get();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<HealthCheckResponse>().Subject;
        response.Status.Should().Be("Healthy");
        response.Environment.Should().Be(Environments.Production);
        response.CheckedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Ready_ReturnsReady_WhenDatabaseCanConnect()
    {
        await using var context = CreateContext();
        var controller = new HealthController(new TestHostEnvironment(Environments.Production), context);

        var result = await controller.Ready();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ReadinessCheckResponse>().Subject;
        response.Status.Should().Be("Ready");
        response.Environment.Should().Be(Environments.Production);
        response.CanConnectToDatabase.Should().BeTrue();
        response.CheckedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
