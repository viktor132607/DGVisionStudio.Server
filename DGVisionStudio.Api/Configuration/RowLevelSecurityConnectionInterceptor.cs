using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DGVisionStudio.Api.Configuration;

// The database's RLS policies consume these values for every application connection.
// Never populate them from request headers, form data or client-supplied role flags.
public sealed class RowLevelSecurityConnectionInterceptor(IHttpContextAccessor accessor)
    : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is not NpgsqlConnection postgres)
            return;
        using var command = CreateSessionCommand(postgres);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is not NpgsqlConnection postgres)
            return;
        await using var command = CreateSessionCommand(postgres);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal NpgsqlCommand CreateSessionCommand(NpgsqlConnection connection)
    {
        var httpContext = accessor.HttpContext;
        var user = httpContext?.User;
        var authenticated = user?.Identity?.IsAuthenticated == true;
        var command = connection.CreateCommand();
        // Set all three on EVERY open, including anonymous requests, so pooled
        // connections cannot carry a previous administrator's session context.
        command.CommandText = """
            SELECT set_config('app.current_user_id', @user_id, false),
                   set_config('app.current_is_admin', @is_admin, false),
                   set_config('app.current_is_system', @is_system, false)
            """;
        command.Parameters.AddWithValue("user_id",
            authenticated ? user!.FindFirstValue(ClaimTypes.NameIdentifier) ?? "" : "");
        command.Parameters.AddWithValue("is_admin",
            authenticated && user!.IsInRole("Admin") ? "true" : "false");
        // Only server startup and background scopes run without an HTTP context.
        command.Parameters.AddWithValue("is_system", httpContext == null ? "true" : "false");
        return command;
    }
}
