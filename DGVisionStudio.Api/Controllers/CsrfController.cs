using DGVisionStudio.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Controllers;

[ApiController]
[Route("api/csrf")]
public class CsrfController : ControllerBase
{
	private const string CsrfCookieName = "DGVisionStudio.Csrf";

	[HttpGet]
	public IActionResult GetToken()
	{
		var token = CsrfProtectionMiddleware.GenerateToken();

		Response.Cookies.Append(CsrfCookieName, token, new CookieOptions
		{
			HttpOnly = false,
			Secure = true,
			SameSite = SameSiteMode.None,
			IsEssential = true,
			Path = "/"
		});

		return Ok(new
		{
			csrfToken = token
		});
	}
}