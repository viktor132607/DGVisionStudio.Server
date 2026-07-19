using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/client/print-requests")]
public class ClientPrintRequestsController : ControllerBase
{
    private readonly IClientPrintRequestEndpointService _service;

    [ActivatorUtilitiesConstructor]
    public ClientPrintRequestsController(IClientPrintRequestEndpointService service)
    {
        _service = service;
    }

    public ClientPrintRequestsController(AppDbContext context)
        : this(new ClientPrintRequestEndpointService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetMine() =>
        this.ToActionResult(await _service.GetMineAsync(User));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMineById(int id) =>
        this.ToActionResult(await _service.GetMineByIdAsync(User, id));

    [HttpPost]
    public async Task<IActionResult> Create(CreatePrintRequestDto dto)
    {
        var result = await _service.CreateAsync(User, dto);
        return result.StatusCode == StatusCodes.Status201Created &&
            result.Value is CreatedPrintRequestResult created
            ? CreatedAtAction(nameof(GetMineById), new { id = created.Id }, new { created.Id })
            : this.ToActionResult(result);
    }
}
