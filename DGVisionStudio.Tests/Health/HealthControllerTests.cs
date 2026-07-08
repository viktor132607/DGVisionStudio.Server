using DGVisionStudio.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Tests.Health;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsHealthyStatus()
    {
        var controller = new HealthController(new TestHostEnvironment(Environments.Production));

        var result = controller.Get();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<HealthCheckResponse>().Subject;
        response.Status.Should().Be("Healthy");
        response.Environment.Should().Be(Environments.Production);
        response.CheckedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
