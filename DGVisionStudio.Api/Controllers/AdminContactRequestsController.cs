using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/contact-requests")]
public class AdminContactRequestsController : ControllerBase
{
    private readonly IContactRequestService _service;

    [ActivatorUtilitiesConstructor]
    public AdminContactRequestsController(IContactRequestService service)
    {
        _service = service;
    }

    public AdminContactRequestsController(AppDbContext context)
        : this(new ContactRequestService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) =>
        this.ToActionResult(await _service.GetAsync(id));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequestDto dto) =>
        this.ToActionResult(await _service.UpdateAsync(id, dto));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateContactRequestDto dto) =>
        this.ToActionResult(await _service.UpdateStatusAsync(id, dto));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        this.ToActionResult(await _service.DeleteAsync(id));
}
