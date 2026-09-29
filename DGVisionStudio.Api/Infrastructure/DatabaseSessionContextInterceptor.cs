using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DGVisionStudio.Api.Infrastructure;

/// <summary>
/// Binds the authenticated application identity to PostgreSQL session settings.
/// PostgreSQL RLS policies consume these values. Npgsql resets pooled connections
/// on close; values are also overwritten every time EF opens a connection.
/// Background services run with the explicit system flag because there is no
/// HttpContext associated with those operations.
/// </summary>
public sealed class DatabaseSessionContextInterceptor(
    IHttpContextAccessor httpContextAccessor) : DbConnectionInterceptor
{
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        ApplyContextAsync(connection, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        ApplyContextAsync(connection, cancellationToken);

    private async Task ApplyContextAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string userId =
            httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        bool isAdmin = httpContext?.User.IsInRole("Admin") == true;
        bool isSystem = httpContext is null;

        await using NpgsqlCommand command = npgsqlConnection.CreateCommand();
        command.CommandText = """
            SELECT
                set_config('app.current_user_id', @user_id, false),
                set_config('app.current_is_admin', @is_admin, false),
                set_config('app.current_is_system', @is_system, false);
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("is_admin", isAdmin ? "true" : "false");
        command.Parameters.AddWithValue("is_system", isSystem ? "true" : "false");

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
