using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountEndpointService _service;

    [ActivatorUtilitiesConstructor]
    public AccountController(IAccountEndpointService service)
    {
        _service = service;
    }

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
        : this(new AccountEndpointService(userManager, signInManager))
    {
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DGVisionStudio.Application.DTOs.Account.DeleteAccountRequest request) =>
        this.ToActionResult(await _service.DeleteAccountAsync(User, request.Password));
}
