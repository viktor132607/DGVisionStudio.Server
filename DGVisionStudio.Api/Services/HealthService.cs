using DGVisionStudio.Api.Controllers;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.Hosting;

namespace DGVisionStudio.Api.Services;

public sealed class HealthService : IHealthService
{
    private readonly IHostEnvironment _environment;
    private readonly AppDbContext _context;

    public HealthService(IHostEnvironment environment, AppDbContext context)
    {
        _environment = environment;
        _context = context;
    }

    public ControllerServiceResult GetHealth() =>
        ControllerServiceResult.Ok(new HealthCheckResponse(
            "Healthy",
            DateTime.UtcNow,
            _environment.EnvironmentName));

    public async Task<ControllerServiceResult> GetReadinessAsync()
    {
        var canConnectToDatabase = await _context.Database.CanConnectAsync();
        if (!canConnectToDatabase)
        {
            return new ControllerServiceResult(
                StatusCodes.Status503ServiceUnavailable,
                new ReadinessCheckResponse(
                    "Unhealthy",
                    DateTime.UtcNow,
                    _environment.EnvironmentName,
                    false));
        }

        return ControllerServiceResult.Ok(new ReadinessCheckResponse(
            "Ready",
            DateTime.UtcNow,
            _environment.EnvironmentName,
            true));
    }
}
