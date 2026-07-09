using System.Text.Json;
using DGVisionStudio.Api.Middleware;
using DGVisionStudio.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Middleware;

public sealed class CsrfProtectionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsStandardForbiddenError_WhenCsrfTokenIsMissing()
    {
        var nextCalled = false;
        var middleware = new CsrfProtectionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<CsrfProtectionMiddleware>.Instance);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-csrf"
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/contact";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().Be("application/json");
        context.Response.Body.Position = 0;

        var response = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        response.Code.Should().Be(ApiErrorCodes.Forbidden);
        response.Message.Should().Be("Invalid CSRF token.");
        response.TraceId.Should().Be("trace-csrf");
    }

    [Fact]
    public async Task InvokeAsync_SkipsValidation_ForSafeMethods()
    {
        var nextCalled = false;
        var middleware = new CsrfProtectionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<CsrfProtectionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/contact";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
