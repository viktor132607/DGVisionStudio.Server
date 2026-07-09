using DGVisionStudio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Configuration;

public static class ApiBehaviorOptionsConfiguration
{
    public static void Configure(ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error => new
                {
                    field = entry.Key,
                    message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Invalid value."
                        : error.ErrorMessage
                }))
                .ToList();

            var details = errors.Count == 0
                ? null
                : System.Text.Json.JsonSerializer.Serialize(errors);

            return ApiError.Validation(
                "Request validation failed.",
                context.HttpContext.TraceIdentifier,
                details);
        };
    }
}
