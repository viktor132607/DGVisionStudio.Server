using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminStatisticsService _service;

    [ActivatorUtilitiesConstructor]
    public AdminDashboardController(IAdminStatisticsService service)
    {
        _service = service;
    }

    public AdminDashboardController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
        : this(new AdminStatisticsService(context, userManager))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetStats() =>
        this.ToActionResult(await _service.GetDashboardStatsAsync());
}
