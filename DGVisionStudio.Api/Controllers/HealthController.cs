using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthService _service;

    [ActivatorUtilitiesConstructor]
    public HealthController(IHealthService service)
    {
        _service = service;
    }

    public HealthController(IHostEnvironment environment, AppDbContext context)
        : this(new HealthService(environment, context))
    {
    }

    [HttpGet]
    public IActionResult Get() =>
        this.ToActionResult(_service.GetHealth());

    [HttpGet("ready")]
    public async Task<IActionResult> Ready() =>
        this.ToActionResult(await _service.GetReadinessAsync());
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
