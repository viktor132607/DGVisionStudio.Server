using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController : ControllerBase
{
    private readonly ISiteSettingsService _service;

    [ActivatorUtilitiesConstructor]
    public SiteSettingsController(ISiteSettingsService service)
    {
        _service = service;
    }

    public SiteSettingsController(AppDbContext context)
        : this(new SiteSettingsService(context))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        this.ToActionResult(await _service.GetAllAsync());
}
