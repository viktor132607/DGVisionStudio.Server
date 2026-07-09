using System.Text.Json;

namespace DGVisionStudio.Api.Models;

public static class ApiErrorResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        string? details = null)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse
        {
            StatusCode = statusCode,
            Code = code,
            Message = message,
            TraceId = context.TraceIdentifier,
            Details = details
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonOptions);
    }
}
