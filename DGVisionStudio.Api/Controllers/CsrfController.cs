using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Controllers;

[ApiController]
[Route("api/csrf")]
public class CsrfController : ControllerBase
{
    private const string CsrfCookieName = "DGVisionStudio.Csrf";
    private readonly ICsrfTokenService _service;

    [ActivatorUtilitiesConstructor]
    public CsrfController(ICsrfTokenService service)
    {
        _service = service;
    }

    public CsrfController()
        : this(new CsrfTokenService())
    {
    }

    [HttpGet]
    public IActionResult GetToken()
    {
        var token = _service.GenerateToken();
        Response.Cookies.Append(CsrfCookieName, token, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Path = "/"
        });

        return Ok(new { csrfToken = token });
    }
}
