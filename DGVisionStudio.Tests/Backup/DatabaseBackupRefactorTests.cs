using System.Text;
using DGVisionStudio.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Backup;

public sealed class DatabaseBackupRefactorTests
{
    private const string Connection =
        "Host=localhost;Database=backup_tests;Username=tester;Password=secret";

    [Fact]
    public void PostgresToolRunner_RejectsIncompleteConnectionConfiguration()
    {
        var action = () => new PostgresToolRunner(
            "Host=localhost;Username=tester",
            NullLogger<PostgresToolRunner>.Instance);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*host, database, and username*");
    }

    [Fact]
    public async Task PostgresToolRunner_ReportsMissingExecutableClearly()
    {
        var runner = new PostgresToolRunner(
            Connection,
            NullLogger<PostgresToolRunner>.Instance);

        var action = () => runner.RunAsync(
            $"missing-postgres-tool-{Guid.NewGuid():N}",
            [],
            "test missing executable");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not installed*PATH*");
    }

    [Fact]
    public async Task ArchiveValidator_RejectsInvalidHeaderBeforeInvokingPgRestore()
    {
        var manager = new DatabaseBackupTempFileManager();
        string path = manager.CreateTemporaryPath("dump");

        try
        {
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("not-pgdump"));
            var validator = new DatabaseBackupArchiveValidator(
                new PostgresToolRunner(
                    Connection,
                    NullLogger<PostgresToolRunner>.Instance));

            var action = () => validator.ValidateRestoreArchiveAsync(path);

            await action.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*PostgreSQL custom-format backup archive*");
        }
        finally
        {
            manager.Delete(path);
        }
    }

    [Fact]
    public async Task TempFileManager_CopiesAndDeletesArchiveWithoutLeakingFiles()
    {
        var manager = new DatabaseBackupTempFileManager();
        byte[] payload = [1, 2, 3, 4, 5];
        await using var source = new MemoryStream(payload);

        string path = await manager.CopyToTemporaryFileAsync(
            source,
            ".dump",
            1024);

        try
        {
            File.Exists(path).Should().BeTrue();
            Path.GetExtension(path).Should().Be(".dump");
            (await File.ReadAllBytesAsync(path)).Should().Equal(payload);
        }
        finally
        {
            manager.Delete(path);
        }

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void TempFileManager_CreatesUniquePaths()
    {
        var manager = new DatabaseBackupTempFileManager();

        string first = manager.CreateTemporaryPath("sql");
        string second = manager.CreateTemporaryPath("sql");

        first.Should().NotBe(second);
        Path.GetExtension(first).Should().Be(".sql");
        Path.GetExtension(second).Should().Be(".sql");
    }
}
