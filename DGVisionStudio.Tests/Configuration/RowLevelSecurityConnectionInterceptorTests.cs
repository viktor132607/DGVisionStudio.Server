using System.Security.Claims;
using DGVisionStudio.Api.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace DGVisionStudio.Tests.Configuration;

public sealed class RowLevelSecurityConnectionInterceptorTests
{
    [Theory]
    [InlineData(true, "Admin", "user-1", "true")]
    [InlineData(true, "User", "user-1", "false")]
    [InlineData(false, "Admin", "", "false")]
    public void RequestContext_UsesOnlyAuthenticatedIdentity(
        bool authenticated, string role, string expectedId, string expectedAdmin)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, role)
        ], authenticated ? "test" : null));
        context.Request.Headers["app.current_is_system"] = "true";
        context.Request.Headers["app.current_is_admin"] = "true";
        var interceptor = new RowLevelSecurityConnectionInterceptor(new HttpContextAccessor { HttpContext = context });
        using var connection = new NpgsqlConnection();
        using var command = interceptor.CreateSessionCommand(connection);
        command.Parameters["user_id"].Value.Should().Be(expectedId);
        command.Parameters["is_admin"].Value.Should().Be(expectedAdmin);
        command.Parameters["is_system"].Value.Should().Be("false");
    }

    [Fact]
    public void BackgroundScope_UsesSystemContext()
    {
        var interceptor = new RowLevelSecurityConnectionInterceptor(new HttpContextAccessor { HttpContext = null });
        using var connection = new NpgsqlConnection();
        using var command = interceptor.CreateSessionCommand(connection);
        command.Parameters["user_id"].Value.Should().Be("");
        command.Parameters["is_admin"].Value.Should().Be("false");
        command.Parameters["is_system"].Value.Should().Be("true");
    }

    [Fact]
    public void ReusedConnection_ResetsAllValuesForNextAnonymousRequest()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim(ClaimTypes.Role, "Admin")
        ], "test"));
        var interceptor = new RowLevelSecurityConnectionInterceptor(accessor);
        using var connection = new NpgsqlConnection();
        using var admin = interceptor.CreateSessionCommand(connection);
        admin.Parameters["is_admin"].Value.Should().Be("true");
        accessor.HttpContext = new DefaultHttpContext();
        using var anonymous = interceptor.CreateSessionCommand(connection);
        anonymous.Parameters["user_id"].Value.Should().Be("");
        anonymous.Parameters["is_admin"].Value.Should().Be("false");
        anonymous.Parameters["is_system"].Value.Should().Be("false");
    }
}
