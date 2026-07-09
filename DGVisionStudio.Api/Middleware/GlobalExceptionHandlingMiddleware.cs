using System.Text.Json;
using DGVisionStudio.Api.Models;

namespace DGVisionStudio.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
	private readonly IWebHostEnvironment _environment;

	public GlobalExceptionHandlingMiddleware(
		RequestDelegate next,
		ILogger<GlobalExceptionHandlingMiddleware> logger,
		IWebHostEnvironment environment)
	{
		_next = next;
		_logger = logger;
		_environment = environment;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (Exception exception)
		{
			await HandleExceptionAsync(context, exception);
		}
	}

	private async Task HandleExceptionAsync(HttpContext context, Exception exception)
	{
		if (context.Response.HasStarted)
		{
			_logger.LogError(exception, "Unhandled exception occurred after the response started.");
			throw exception;
		}

		var statusCode = GetStatusCode(exception);
		var code = GetErrorCode(statusCode);

		_logger.LogError(
			exception,
			"Unhandled exception. Method: {Method}, Path: {Path}, StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}",
			context.Request.Method,
			context.Request.Path,
			statusCode,
			code,
			context.TraceIdentifier);

		context.Response.Clear();
		context.Response.StatusCode = statusCode;
		context.Response.ContentType = "application/json";

		var response = new ApiErrorResponse
		{
			StatusCode = statusCode,
			Code = code,
			Message = GetPublicMessage(statusCode),
			TraceId = context.TraceIdentifier,
			Details = _environment.IsDevelopment() ? exception.Message : null
		};

		var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		await context.Response.WriteAsync(json);
	}

	private static int GetStatusCode(Exception exception)
	{
		return exception switch
		{
			UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
			KeyNotFoundException => StatusCodes.Status404NotFound,
			ArgumentException => StatusCodes.Status400BadRequest,
			InvalidOperationException => StatusCodes.Status400BadRequest,
			_ => StatusCodes.Status500InternalServerError
		};
	}

	private static string GetErrorCode(int statusCode)
	{
		return statusCode switch
		{
			400 => ApiErrorCodes.ValidationError,
			401 => ApiErrorCodes.Unauthorized,
			403 => ApiErrorCodes.Forbidden,
			404 => ApiErrorCodes.NotFound,
			_ => ApiErrorCodes.UnexpectedError
		};
	}

	private static string GetPublicMessage(int statusCode)
	{
		return statusCode switch
		{
			400 => "Invalid request.",
			401 => "Unauthorized.",
			403 => "Forbidden.",
			404 => "Resource not found.",
			_ => "An unexpected error occurred."
		};
	}
}
