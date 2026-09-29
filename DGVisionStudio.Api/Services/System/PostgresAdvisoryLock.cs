using Npgsql;

namespace DGVisionStudio.Api.Services;

/// <summary>
/// Cross-instance PostgreSQL advisory lock used by background jobs and destructive
/// maintenance operations. The lock is held for the lifetime of a dedicated
/// PostgreSQL connection, so it is automatically released if an instance dies.
/// </summary>
public sealed class PostgresAdvisoryLock(
    string connectionString,
    ILogger<PostgresAdvisoryLock> logger)
{
    public async Task<IAsyncDisposable> AcquireAsync(
        long key,
        CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using NpgsqlCommand command =
                new("SELECT pg_advisory_lock(@key);", connection);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new Lease(connection, key, logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        long key,
        CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using NpgsqlCommand command =
                new("SELECT pg_try_advisory_lock(@key);", connection);
            command.Parameters.AddWithValue("key", key);

            bool acquired =
                (bool?)await command.ExecuteScalarAsync(cancellationToken) == true;

            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new Lease(connection, key, logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(
        NpgsqlConnection connection,
        long key,
        ILogger logger) : IAsyncDisposable
    {
        private int disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    await using NpgsqlCommand command =
                        new("SELECT pg_advisory_unlock(@key);", connection);
                    command.Parameters.AddWithValue("key", key);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to explicitly release PostgreSQL advisory lock {AdvisoryLockKey}. The connection close will release it.",
                    key);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
