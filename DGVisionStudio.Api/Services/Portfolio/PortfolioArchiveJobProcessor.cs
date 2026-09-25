using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioArchiveJobProcessor(
    PortfolioArchiveJobQueue queue,
    IServiceScopeFactory scopes,
    ILogger<PortfolioArchiveJobs> logger)
{
    internal async Task ProcessAsync(
        CancellationToken token)
    {
        await foreach (
            var job in queue.ReadAllAsync(token))
        {
            try
            {
                Volatile.Write(
                    ref job.State,
                    job.State with
                    {
                        Status = "preparing",
                        ExpiresAt =
                            DateTimeOffset.UtcNow.AddHours(2)
                    });

                using var timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        token);
                timeout.CancelAfter(
                    TimeSpan.FromHours(2));

                await using var scope =
                    scopes.CreateAsyncScope();

                var builder =
                    scope.ServiceProvider
                        .GetRequiredService<
                            PortfolioArchiveBuilder>();

                job.File = await builder.BuildAsync(
                    job.AlbumIds,
                    progress =>
                        Volatile.Write(
                            ref job.State,
                            job.State with
                            {
                                Status = progress.Status,
                                CompletedFiles =
                                    progress.CompletedFiles,
                                TotalFiles =
                                    progress.TotalFiles
                            }),
                    timeout.Token);

                Volatile.Write(
                    ref job.State,
                    job.State with
                    {
                        Status = "ready",
                        FileName = job.File.FileName,
                        ExpiresAt =
                            DateTimeOffset.UtcNow.AddHours(1)
                    });
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Archive job {JobId} failed",
                    job.State.Id);

                Volatile.Write(
                    ref job.State,
                    job.State with
                    {
                        Status = "failed",
                        ExpiresAt =
                            DateTimeOffset.UtcNow.AddHours(1),
                        Error =
                            ex is ArchiveRequestException
                                ? ex.Message
                                : ex is OperationCanceledException
                                    ? "Подготовката на архива беше прекъсната. Опитай отново."
                                    : "Архивът не можа да бъде създаден. Провери файловете и опитай отново."
                    });
            }
        }
    }
}
