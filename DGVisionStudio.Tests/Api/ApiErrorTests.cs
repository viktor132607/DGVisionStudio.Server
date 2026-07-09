using System.Text.Json;
using DGVisionStudio.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Api;

public sealed class ApiErrorTests
{
    [Fact]
    public void Validation_ReturnsStandardBadRequestPayload()
    {
        var result = ApiError.Validation("Invalid title.", "trace-1");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var response = result.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        response.Code.Should().Be(ApiErrorCodes.ValidationError);
        response.Message.Should().Be("Invalid title.");
        response.TraceId.Should().Be("trace-1");
    }

    [Fact]
    public void Unauthorized_ReturnsStandardUnauthorizedPayload()
    {
        var result = ApiError.Unauthorized(traceId: "trace-2");

        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var response = result.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        response.Code.Should().Be(ApiErrorCodes.Unauthorized);
        response.Message.Should().Be("Unauthorized.");
        response.TraceId.Should().Be("trace-2");
    }

    [Fact]
    public void NotFound_ReturnsStandardNotFoundPayload()
    {
        var result = ApiError.NotFound(traceId: "trace-3");

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var response = result.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        response.Code.Should().Be(ApiErrorCodes.NotFound);
        response.Message.Should().Be("Resource not found.");
        response.TraceId.Should().Be("trace-3");
    }

    [Fact]
    public void Create_ReturnsCustomStatusCodeAndPayload()
    {
        var result = ApiError.Create(
            StatusCodes.Status409Conflict,
            ApiErrorCodes.Conflict,
            "Conflict.",
            "trace-4",
            "Details");

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var response = result.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        response.Code.Should().Be(ApiErrorCodes.Conflict);
        response.Message.Should().Be("Conflict.");
        response.TraceId.Should().Be("trace-4");
        response.Details.Should().Be("Details");
    }

    [Fact]
    public async Task ApiErrorResponseWriter_WritesStandardJsonPayload()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-writer"
        };
        context.Response.Body = new MemoryStream();

        await ApiErrorResponseWriter.WriteAsync(
            context,
            StatusCodes.Status403Forbidden,
            ApiErrorCodes.Forbidden,
            "Forbidden.");

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().Be("application/json");
        context.Response.Body.Position = 0;

        var response = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        response.Code.Should().Be(ApiErrorCodes.Forbidden);
        response.Message.Should().Be("Forbidden.");
        response.TraceId.Should().Be("trace-writer");
    }
}
