using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DGVisionStudio.Api.Services;

public sealed record DatabaseBackupArtifact(string FilePath, string FileName);

public sealed class DatabaseBackupService
{
    private const int CopyBufferSize = 128 * 1024;

    private readonly PostgresToolRunner toolRunner;
    private readonly DatabaseBackupArchiveValidator archiveValidator;
    private readonly DatabaseBackupTempFileManager tempFiles;
    private readonly ILogger<DatabaseBackupService> logger;
    private readonly SemaphoreSlim operationLock = new(1, 1);

    public DatabaseBackupService(
        PostgresToolRunner toolRunner,
        DatabaseBackupArchiveValidator archiveValidator,
        DatabaseBackupTempFileManager tempFiles,
        ILogger<DatabaseBackupService> logger)
    {
        this.toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
        this.archiveValidator = archiveValidator ?? throw new ArgumentNullException(nameof(archiveValidator));
        this.tempFiles = tempFiles ?? throw new ArgumentNullException(nameof(tempFiles));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DatabaseBackupService(
        string connectionString,
        ILogger<DatabaseBackupService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var runner = new PostgresToolRunner(
            connectionString,
            NullLogger<PostgresToolRunner>.Instance);

        toolRunner = runner;
        archiveValidator = new DatabaseBackupArchiveValidator(runner);
        tempFiles = new DatabaseBackupTempFileManager();
        this.logger = logger;
    }

    public async Task<DatabaseBackupArtifact> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        string? backupPath = null;

        try
        {
            backupPath = tempFiles.CreateTemporaryPath("dump");

            await toolRunner.RunAsync(
                "pg_dump",
                [
                    "--format=custom",
                    "--compress=9",
                    "--file",
                    backupPath,
                    toolRunner.DatabaseName
                ],
                "create the database backup",
                cancellationToken);

            await archiveValidator.ValidateHeaderAsync(backupPath, cancellationToken);

            FileInfo backupFile = new(backupPath);
            if (!backupFile.Exists || backupFile.Length == 0)
            {
                throw new InvalidOperationException(
                    "PostgreSQL reported a successful backup, but the generated archive is empty.");
            }

            string downloadName =
                $"dgvisionstudio-full-database-{DateTime.UtcNow:yyyyMMdd-HHmmss}Z.dump";

            logger.LogInformation(
                "Full PostgreSQL backup created successfully ({BackupSize} bytes).",
                backupFile.Length);

            return new DatabaseBackupArtifact(backupPath, downloadName);
        }
        catch
        {
            tempFiles.Delete(backupPath);
            throw;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task RestoreBackupAsync(
        Stream archive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);

        await operationLock.WaitAsync(cancellationToken);
        string? uploadedPath = null;
        string? sqlPath = null;
        string? resetPath = null;

        try
        {
            uploadedPath = await tempFiles.CopyToTemporaryFileAsync(
                archive,
                "dump",
                CopyBufferSize,
                cancellationToken);

            FileInfo uploadedFile = new(uploadedPath);
            if (uploadedFile.Length == 0)
                throw new InvalidDataException("The uploaded backup archive is empty.");

            await archiveValidator.ValidateRestoreArchiveAsync(uploadedPath, cancellationToken);

            sqlPath = tempFiles.CreateTemporaryPath("sql");
            resetPath = tempFiles.CreateTemporaryPath("sql");

            await toolRunner.RunAsync(
                "pg_restore",
                ["--clean", "--if-exists", "--no-owner", "--no-privileges", "--file", sqlPath, uploadedPath],
                "read the complete database archive",
                cancellationToken);

            await File.WriteAllTextAsync(resetPath, ResetDatabaseSql, cancellationToken);

            NpgsqlConnection.ClearAllPools();

            try
            {
                await toolRunner.RunAsync(
                    "psql",
                    [
                        "--no-psqlrc",
                        "--no-password",
                        "--single-transaction",
                        "--set=ON_ERROR_STOP=on",
                        "--file",
                        resetPath,
                        "--file",
                        sqlPath
                    ],
                    "restore the database backup",
                    cancellationToken);
            }
            finally
            {
                NpgsqlConnection.ClearAllPools();
            }

            logger.LogWarning(
                "Full PostgreSQL database restore completed successfully from an uploaded archive ({BackupSize} bytes).",
                uploadedFile.Length);
        }
        finally
        {
            tempFiles.DeleteAll(uploadedPath, sqlPath, resetPath);
            operationLock.Release();
        }
    }

    private const string ResetDatabaseSql = """
        SET LOCAL lock_timeout = '30s';
        SELECT pg_advisory_xact_lock(723480193);
        DO $reset$
        DECLARE item record;
        BEGIN
            FOR item IN SELECT extname FROM pg_extension WHERE extname <> 'plpgsql'
            LOOP EXECUTE format('DROP EXTENSION %I CASCADE', item.extname); END LOOP;
            FOR item IN SELECT nspname FROM pg_namespace
                WHERE nspname <> 'information_schema' AND nspname !~ '^pg_'
            LOOP EXECUTE format('DROP SCHEMA %I CASCADE', item.nspname); END LOOP;
            PERFORM lo_unlink(oid) FROM pg_largeobject_metadata;
        END $reset$;
        CREATE SCHEMA public;
        GRANT USAGE ON SCHEMA public TO PUBLIC;
        """;
}
