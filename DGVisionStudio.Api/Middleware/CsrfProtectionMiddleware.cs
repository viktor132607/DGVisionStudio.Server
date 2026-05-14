using System.Security.Cryptography;

namespace DGVisionStudio.Api.Middleware;

public class CsrfProtectionMiddleware
{
	private const string CsrfCookieName = "DGVisionStudio.Csrf";
	private const string CsrfHeaderName = "X-CSRF-TOKEN";

	private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
	{
		HttpMethods.Get,
		HttpMethods.Head,
		HttpMethods.Options,
		HttpMethods.Trace
	};

	private readonly RequestDelegate _next;
	private readonly ILogger<CsrfProtectionMiddleware> _logger;

	public CsrfProtectionMiddleware(
		RequestDelegate next,
		ILogger<CsrfProtectionMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (SafeMethods.Contains(context.Request.Method))
		{
			await _next(context);
			return;
		}

		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await _next(context);
			return;
		}

		if (context.Request.Path.StartsWithSegments("/api/csrf"))
		{
			await _next(context);
			return;
		}

		var cookieToken = context.Request.Cookies[CsrfCookieName];
		var headerToken = context.Request.Headers[CsrfHeaderName].FirstOrDefault();

		if (string.IsNullOrWhiteSpace(cookieToken) ||
			string.IsNullOrWhiteSpace(headerToken) ||
			!FixedTimeEquals(cookieToken, headerToken))
		{
			_logger.LogWarning(
				"CSRF validation failed. Method: {Method}, Path: {Path}, TraceId: {TraceId}",
				context.Request.Method,
				context.Request.Path,
				context.TraceIdentifier);

			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			context.Response.ContentType = "application/json";

			await context.Response.WriteAsJsonAsync(new
			{
				statusCode = StatusCodes.Status403Forbidden,
				message = "Invalid CSRF token.",
				traceId = context.TraceIdentifier
			});

			return;
		}

		await _next(context);
	}

	public static string GenerateToken()
	{
		var bytes = RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes);
	}

	private static bool FixedTimeEquals(string first, string second)
	{
		var firstBytes = first.ConvertFromBase64StringSafe();
		var secondBytes = second.ConvertFromBase64StringSafe();

		if (firstBytes.Length == 0 || secondBytes.Length == 0)
			return false;

		return CryptographicOperations.FixedTimeEquals(firstBytes, secondBytes);
	}
}

public static class CsrfStringExtensions
{
	public static byte[] ConvertFromBase64StringSafe(this string value)
	{
		try
		{
			return Convert.FromBase64String(value);
		}
		catch
		{
			return Array.Empty<byte>();
		}
	}
}