using System.Text;

namespace DGVisionStudio.Api.Services;

public sealed class DatabaseBackupArchiveValidator(PostgresToolRunner toolRunner)
{
    private static readonly byte[] PgDumpMagic = Encoding.ASCII.GetBytes("PGDMP");

    public async Task ValidateHeaderAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            PgDumpMagic.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] header = new byte[PgDumpMagic.Length];
        int totalRead = 0;

        while (totalRead < header.Length)
        {
            int read = await stream.ReadAsync(
                header.AsMemory(totalRead, header.Length - totalRead),
                cancellationToken);

            if (read == 0)
                break;

            totalRead += read;
        }

        if (totalRead != PgDumpMagic.Length || !header.SequenceEqual(PgDumpMagic))
        {
            throw new InvalidDataException(
                "The file is not a PostgreSQL custom-format backup archive.");
        }
    }

    public async Task ValidateRestoreArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await ValidateHeaderAsync(filePath, cancellationToken);

        try
        {
            await toolRunner.RunAsync(
                "pg_restore",
                ["--list", filePath],
                "validate the uploaded database backup",
                cancellationToken);
        }
        catch (PostgresToolException ex)
        {
            throw new InvalidDataException(
                "The uploaded file is not a valid PostgreSQL custom-format backup archive.",
                ex);
        }
    }
}
