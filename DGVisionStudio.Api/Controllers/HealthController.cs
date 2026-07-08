using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new HealthCheckResponse(
            "Healthy",
            DateTime.UtcNow,
            environment.EnvironmentName));
    }
}

public sealed record HealthCheckResponse(
    string Status,
    DateTime CheckedAtUtc,
    string Environment);
