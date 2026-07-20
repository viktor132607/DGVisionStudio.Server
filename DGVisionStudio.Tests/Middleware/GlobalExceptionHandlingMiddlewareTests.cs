using System.Text.Json;
using DGVisionStudio.Api.Middleware;
using DGVisionStudio.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Middleware;

public sealed class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsUnexpectedError_WhenInvalidOperationExceptionEscapes()
    {
        var response = await InvokeAsync(
            new InvalidOperationException("ambiguous service constructor"),
            environmentName: "Production");

        response.Context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        response.Body.Code.Should().Be(ApiErrorCodes.UnexpectedError);
        response.Body.Message.Should().Be("An unexpected error occurred.");
        response.Body.Details.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsValidationError_ForArgumentException()
    {
        var response = await InvokeAsync(
            new ArgumentException("invalid input"),
            environmentName: "Production");

        response.Context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        response.Body.Code.Should().Be(ApiErrorCodes.ValidationError);
        response.Body.Message.Should().Be("Invalid request.");
    }

    [Fact]
    public async Task InvokeAsync_IncludesExceptionDetails_OnlyInDevelopment()
    {
        var response = await InvokeAsync(
            new InvalidOperationException("diagnostic detail"),
            environmentName: "Development");

        response.Context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        response.Body.Details.Should().Be("diagnostic detail");
    }

    private static async Task<(DefaultHttpContext Context, ApiErrorResponse Body)> InvokeAsync(
        Exception exception,
        string environmentName)
    {
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => Task.FromException(exception),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance,
            new TestWebHostEnvironment { EnvironmentName = environmentName });

        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-exception"
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        body.Should().NotBeNull();
        body!.TraceId.Should().Be("trace-exception");
        return (context, body);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DGVisionStudio.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
