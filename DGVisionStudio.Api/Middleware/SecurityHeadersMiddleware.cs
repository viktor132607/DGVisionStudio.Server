namespace DGVisionStudio.Api.Middleware;

public class SecurityHeadersMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IConfiguration _configuration;

	public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
	{
		_next = next;
		_configuration = configuration;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		context.Response.OnStarting(() =>
		{
			var headers = context.Response.Headers;

			var frontendUrl = _configuration["Frontend:Url"]?.TrimEnd('/');
			var apiUrl = _configuration["Api:Url"]?.TrimEnd('/');

			var cspOrigins = new[]
			{
				frontendUrl,
				apiUrl
			}
			.Where(origin => !string.IsNullOrWhiteSpace(origin))
			.Distinct(StringComparer.OrdinalIgnoreCase);

			var origins = string.Join(" ", cspOrigins);

			headers["X-Content-Type-Options"] = "nosniff";
			headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
			headers["X-Frame-Options"] = "DENY";
			headers["Permissions-Policy"] =
				"camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)";

			headers["Content-Security-Policy"] =
				"default-src 'self'; " +
				"script-src 'self'; " +
				"style-src 'self' 'unsafe-inline'; " +
				"img-src 'self' data: blob: https:; " +
				"font-src 'self' data:; " +
				$"connect-src 'self' {origins}; " +
				"media-src 'self' blob:; " +
				"object-src 'none'; " +
				"base-uri 'self'; " +
				"frame-ancestors 'none'; " +
				"form-action 'self'; " +
				"upgrade-insecure-requests";

			return Task.CompletedTask;
		});

		await _next(context);
	}
}