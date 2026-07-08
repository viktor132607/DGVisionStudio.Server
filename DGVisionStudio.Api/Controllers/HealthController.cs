using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(
    IHostEnvironment environment,
    AppDbContext context) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new HealthCheckResponse(
            "Healthy",
            DateTime.UtcNow,
            environment.EnvironmentName));
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var canConnectToDatabase = await context.Database.CanConnectAsync();

        if (!canConnectToDatabase)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ReadinessCheckResponse(
                    "Unhealthy",
                    DateTime.UtcNow,
                    environment.EnvironmentName,
                    false));
        }

        return Ok(new ReadinessCheckResponse(
            "Ready",
            DateTime.UtcNow,
            environment.EnvironmentName,
            true));
    }
}

public sealed record HealthCheckResponse(
    string Status,
    DateTime CheckedAtUtc,
    string Environment);

public sealed record ReadinessCheckResponse(
    string Status,
    DateTime CheckedAtUtc,
    string Environment,
    bool CanConnectToDatabase);
