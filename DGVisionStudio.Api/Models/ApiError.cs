using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Models;

public static class ApiError
{
    public static ObjectResult Create(
        int statusCode,
        string code,
        string message,
        string? traceId = null,
        string? details = null)
    {
        return new ObjectResult(new ApiErrorResponse
        {
            StatusCode = statusCode,
            Code = code,
            Message = message,
            TraceId = traceId ?? string.Empty,
            Details = details
        })
        {
            StatusCode = statusCode
        };
    }

    public static BadRequestObjectResult Validation(string message, string? traceId = null, string? details = null)
    {
        return new BadRequestObjectResult(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Code = ApiErrorCodes.ValidationError,
            Message = message,
            TraceId = traceId ?? string.Empty,
            Details = details
        });
    }

    public static UnauthorizedObjectResult Unauthorized(string message = "Unauthorized.", string? traceId = null)
    {
        return new UnauthorizedObjectResult(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            Code = ApiErrorCodes.Unauthorized,
            Message = message,
            TraceId = traceId ?? string.Empty
        });
    }

    public static NotFoundObjectResult NotFound(string message = "Resource not found.", string? traceId = null)
    {
        return new NotFoundObjectResult(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Code = ApiErrorCodes.NotFound,
            Message = message,
            TraceId = traceId ?? string.Empty
        });
    }
}
