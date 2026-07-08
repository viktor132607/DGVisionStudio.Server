using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace DGVisionStudio.Api.Configuration;

public sealed class ResolvedDatabaseConnection(string connectionString, string sourceKey)
{
    public string ConnectionString { get; } = connectionString;

    public string SourceKey { get; } = sourceKey;
}

public static class DatabaseConnectionStringResolver
{
    private static readonly string[] CandidateKeys =
    [
        "DATABASE_URL",
        "POSTGRES_URL",
        "ConnectionStrings:DefaultConnection",
        "ConnectionStrings:Postgres"
    ];

    public static ResolvedDatabaseConnection Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var candidateKey in CandidateKeys)
        {
            var rawValue = configuration[candidateKey];

            if (string.IsNullOrWhiteSpace(rawValue))
                continue;

            var normalizedConnectionString = NormalizeAndValidate(rawValue, candidateKey, environment);
            return new ResolvedDatabaseConnection(normalizedConnectionString, candidateKey);
        }

        throw new InvalidOperationException(
            $"No PostgreSQL connection string is configured. Checked keys in order: {string.Join(", ", CandidateKeys)}.");
    }

    public static string NormalizeAndValidate(string rawValue, string sourceKey, IHostEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentNullException.ThrowIfNull(environment);

        var sanitizedValue = UnwrapBalancedQuotes(rawValue.Trim());

        if (string.IsNullOrWhiteSpace(sanitizedValue))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL connection string from '{sourceKey}' is empty after trimming quotes and whitespace.");
        }

        var connectionStringBuilder = IsPostgresUrl(sanitizedValue)
            ? BuildFromPostgresUrl(sanitizedValue, sourceKey)
            : BuildFromConnectionString(sanitizedValue, sourceKey);

        EnsureConnectionStringIsSafeForEnvironment(connectionStringBuilder, sourceKey, environment);

        return connectionStringBuilder.ConnectionString;
    }

    private static string UnwrapBalancedQuotes(string value)
    {
        if (value.Length >= 2)
        {
            var isDoubleQuoted = value[0] == '"' && value[^1] == '"';
            var isSingleQuoted = value[0] == '\'' && value[^1] == '\'';

            if (isDoubleQuoted || isSingleQuoted)
                return value[1..^1].Trim();
        }

        return value;
    }

    private static bool IsPostgresUrl(string value)
    {
        return value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
    }

    private static NpgsqlConnectionStringBuilder BuildFromConnectionString(string value, string sourceKey)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL connection value from '{sourceKey}' is not a valid Npgsql connection string or postgres URL.",
                ex);
        }
    }

    private static NpgsqlConnectionStringBuilder BuildFromPostgresUrl(string value, string sourceKey)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var databaseUri))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL URL from '{sourceKey}' is not a valid absolute URI.");
        }

        if (!databaseUri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            && !databaseUri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL URL from '{sourceKey}' must start with postgres:// or postgresql://.");
        }

        var credentials = databaseUri.UserInfo.Split(':', 2, StringSplitOptions.None);
        var databaseName = Uri.UnescapeDataString(databaseUri.AbsolutePath.Trim('/'));

        if (string.IsNullOrWhiteSpace(databaseUri.Host)
            || credentials.Length != 2
            || string.IsNullOrWhiteSpace(credentials[0])
            || string.IsNullOrWhiteSpace(credentials[1])
            || string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL URL from '{sourceKey}' must include host, database, username, and password.");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
            Database = databaseName,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        };

        if (!string.IsNullOrWhiteSpace(databaseUri.Query))
            ApplyQueryParameters(connectionStringBuilder, databaseUri.Query, sourceKey);

        return connectionStringBuilder;
    }

    private static void ApplyQueryParameters(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string queryString,
        string sourceKey)
    {
        var trimmedQueryString = queryString.TrimStart('?');

        foreach (var parameter in trimmedQueryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2, StringSplitOptions.None);
            var rawKey = Uri.UnescapeDataString(pair[0]);
            var rawValue = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;

            if (string.IsNullOrWhiteSpace(rawKey))
                continue;

            var normalizedKey = NormalizeKeyword(rawKey);

            try
            {
                connectionStringBuilder[normalizedKey] = rawValue;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"The PostgreSQL URL from '{sourceKey}' contains unsupported query parameter '{rawKey}'.",
                    ex);
            }
        }
    }

    private static string NormalizeKeyword(string keyword)
    {
        var comparableKeyword = new string(keyword.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        return comparableKeyword switch
        {
            "SSLMODE" => "SSL Mode",
            "TRUSTSERVERCERTIFICATE" => "Trust Server Certificate",
            "COMMANDTIMEOUT" => "Command Timeout",
            "TIMEOUT" => "Timeout",
            "SEARCHPATH" => "Search Path",
            "KEEPALIVE" => "Keepalive",
            _ => keyword
        };
    }

    private static void EnsureConnectionStringIsSafeForEnvironment(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string sourceKey,
        IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Host))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL connection value from '{sourceKey}' does not define a host.");
        }

        if (environment.IsDevelopment())
            return;

        if (IsLoopbackHost(connectionStringBuilder.Host))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL connection value from '{sourceKey}' resolves to '{connectionStringBuilder.Host}', which is a local development placeholder. Configure a real database connection for '{environment.EnvironmentName}'.");
        }
    }

    private static bool IsLoopbackHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
