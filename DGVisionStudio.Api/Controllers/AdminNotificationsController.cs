using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly IAdminStatisticsService _service;

    [ActivatorUtilitiesConstructor]
    public AdminNotificationsController(IAdminStatisticsService service)
    {
        _service = service;
    }

    public AdminNotificationsController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
        : this(new AdminStatisticsService(context, userManager))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetCounts() =>
        this.ToActionResult(await _service.GetNotificationCountsAsync());
}
