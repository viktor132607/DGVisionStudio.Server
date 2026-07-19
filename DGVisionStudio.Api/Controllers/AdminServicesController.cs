using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/services")]
public class AdminServicesController : ControllerBase
{
    private readonly IServiceCatalogService _service;

    [ActivatorUtilitiesConstructor]
    public AdminServicesController(IServiceCatalogService service)
    {
        _service = service;
    }

    public AdminServicesController(AppDbContext context)
        : this(new ServiceCatalogService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        this.ToActionResult(await _service.GetAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceCardDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return result.StatusCode == StatusCodes.Status201Created && result.Value is Service item
            ? CreatedAtAction(nameof(Get), new { id = item.Id }, item)
            : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceCardDto dto) =>
        this.ToActionResult(await _service.UpdateAsync(id, dto));

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderServicesDto dto) =>
        this.ToActionResult(await _service.ReorderAsync(dto));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        this.ToActionResult(await _service.DeleteAsync(id));
}

public class ServiceCardDto
{
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ReorderServicesDto
{
    public List<int> Ids { get; set; } = [];
}
