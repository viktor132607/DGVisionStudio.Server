using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _service;

    [ActivatorUtilitiesConstructor]
    public ServicesController(IServiceCatalogService service)
    {
        _service = service;
    }

    public ServicesController(AppDbContext context)
        : this(new ServiceCatalogService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetActiveAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        this.ToActionResult(await _service.GetPublicByIdAsync(id));
}
