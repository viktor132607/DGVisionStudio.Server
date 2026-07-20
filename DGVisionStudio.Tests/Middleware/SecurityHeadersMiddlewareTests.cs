using DGVisionStudio.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DGVisionStudio.Tests.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsSecurityHeadersAndConfiguredCspOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Url"] = "https://dgvisionstudio.com/",
                ["Api:Url"] = "https://api.dgvisionstudio.com/"
            })
            .Build();
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(
            async context =>
            {
                nextCalled = true;
                await context.Response.StartAsync();
            },
            configuration);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        context.Response.Headers["Permissions-Policy"].ToString().Should().Contain("camera=()");

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("connect-src 'self' https://dgvisionstudio.com https://api.dgvisionstudio.com");
        csp.Should().Contain("frame-ancestors 'none'");
    }
}
