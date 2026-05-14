namespace DGVisionStudio.Api.Middleware;

public class SecurityHeadersMiddleware
{
	private readonly RequestDelegate _next;

	public SecurityHeadersMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		context.Response.OnStarting(() =>
		{
			var headers = context.Response.Headers;

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
				"connect-src 'self' http://localhost:10000 https://dgvisionstudio.com https://www.dgvisionstudio.com; " +
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