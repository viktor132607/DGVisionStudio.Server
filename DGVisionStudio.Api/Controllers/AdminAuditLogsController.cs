using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/audit-logs")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IAdminAuditLogQueryService _service;

    [ActivatorUtilitiesConstructor]
    public AdminAuditLogsController(IAdminAuditLogQueryService service)
    {
        _service = service;
    }

    public AdminAuditLogsController(AppDbContext context)
        : this(new AdminAuditLogQueryService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? adminEmail = null,
        [FromQuery] string? action = null) =>
        this.ToActionResult(await _service.GetAuditLogsAsync(
            page,
            pageSize,
            entityType,
            entityId,
            adminEmail,
            action));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAuditLogById([FromRoute] int id) =>
        this.ToActionResult(await _service.GetAuditLogByIdAsync(id));
}
