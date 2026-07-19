using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/debug/users")]
public class DebugUsersController : ControllerBase
{
    private readonly IDebugUserService _service;

    [ActivatorUtilitiesConstructor]
    public DebugUsersController(IDebugUserService service)
    {
        _service = service;
    }

    public DebugUsersController(UserManager<ApplicationUser> userManager)
        : this(new DebugUserService(userManager))
    {
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers() =>
        this.ToActionResult(await _service.GetUsersAsync());
}
