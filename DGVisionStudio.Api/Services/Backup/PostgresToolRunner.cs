using System.ComponentModel;
using System.Diagnostics;
using Npgsql;

namespace DGVisionStudio.Api.Services;

public sealed class PostgresToolRunner
{
    private readonly NpgsqlConnectionStringBuilder connection;
    private readonly ILogger<PostgresToolRunner> logger;

    public PostgresToolRunner(
        string connectionString,
        ILogger<PostgresToolRunner> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        connection = new NpgsqlConnectionStringBuilder(connectionString);
        this.logger = logger;

        if (string.IsNullOrWhiteSpace(connection.Host) ||
            string.IsNullOrWhiteSpace(connection.Database) ||
            string.IsNullOrWhiteSpace(connection.Username))
        {
            throw new InvalidOperationException(
                "Database backup requires PostgreSQL host, database, and username configuration.");
        }
    }

    public string DatabaseName => connection.Database!;

    public async Task RunAsync(
        string executable,
        IReadOnlyCollection<string> arguments,
        string operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        ApplyEnvironment(startInfo);

        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Unable to start {executable} while trying to {operation}.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL utility '{executable}' is not installed or is not available in PATH.",
                ex);
        }

        Task standardOutputTask = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(standardOutputTask, standardErrorTask);
            throw;
        }

        await standardOutputTask;
        string standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new PostgresToolException(
                executable,
                operation,
                process.ExitCode,
                standardError);
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            logger.LogDebug(
                "{PostgresTool} completed while trying to {Operation}: {ToolOutput}",
                executable,
                operation,
                standardError.Trim());
        }
    }

    private void ApplyEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment["PGCONNECT_TIMEOUT"] = "30";
        startInfo.Environment["PGHOST"] = connection.Host;
        startInfo.Environment["PGPORT"] = connection.Port.ToString();
        startInfo.Environment["PGDATABASE"] = connection.Database;
        startInfo.Environment["PGUSER"] = connection.Username;

        if (!string.IsNullOrEmpty(connection.Password))
            startInfo.Environment["PGPASSWORD"] = connection.Password;

        string sslMode = connection.SslMode.ToString();
        startInfo.Environment["PGSSLMODE"] = sslMode switch
        {
            "VerifyCA" => "verify-ca",
            "VerifyFull" => "verify-full",
            _ => sslMode.ToLowerInvariant()
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cancellation cleanup only.
        }
    }
}

public sealed class PostgresToolException(
    string executable,
    string operation,
    int exitCode,
    string standardError)
    : InvalidOperationException(
        $"{executable} failed to {operation} with exit code {exitCode}: " +
        (string.IsNullOrWhiteSpace(standardError)
            ? "No error details were returned by PostgreSQL."
            : standardError.Trim()))
{
}
