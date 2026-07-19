using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/print-requests")]
public class AdminPrintRequestsController : ControllerBase
{
    private readonly IAdminPrintRequestService _service;

    [ActivatorUtilitiesConstructor]
    public AdminPrintRequestsController(IAdminPrintRequestService service)
    {
        _service = service;
    }

    public AdminPrintRequestsController(AppDbContext context)
        : this(new AdminPrintRequestService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        this.ToActionResult(await _service.GetByIdAsync(id));

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdatePrintRequestStatusDto dto) =>
        this.ToActionResult(await _service.UpdateStatusAsync(id, dto));

    [HttpPut("{id:int}/seen")]
    public async Task<IActionResult> MarkSeen(int id) =>
        this.ToActionResult(await _service.MarkSeenAsync(id));

    [HttpPut("seen")]
    public async Task<IActionResult> MarkAllSeen() =>
        this.ToActionResult(await _service.MarkAllSeenAsync());

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        this.ToActionResult(await _service.DeleteAsync(id));
}
