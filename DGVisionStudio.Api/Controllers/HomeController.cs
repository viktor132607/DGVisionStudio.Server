using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/home")]
public class HomeController : ControllerBase
{
    private readonly IHomeStatusService _service;

    [ActivatorUtilitiesConstructor]
    public HomeController(IHomeStatusService service)
    {
        _service = service;
    }

    public HomeController()
        : this(new HomeStatusService())
    {
    }

    [HttpGet]
    public IActionResult Get() =>
        this.ToActionResult(_service.GetStatus());
}
