using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DGVisionStudio.Api.Services;

public sealed record ArchiveJobStatus(Guid Id, string Status, int CompletedFiles, int TotalFiles,
    string? FileName, string? Error, DateTimeOffset ExpiresAt);

/// <summary>Single disk-backed export worker. Jobs survive HTTP timeouts and belong to their requesting admin.</summary>
public sealed class PortfolioArchiveJobs(IServiceScopeFactory scopes, ILogger<PortfolioArchiveJobs> logger) : BackgroundService
{
    private sealed class Job(string owner, int[]? ids)
    {
        public string Owner { get; } = owner;
        public int[]? AlbumIds { get; } = ids;
        public ArchiveJobStatus State = new(Guid.NewGuid(), "queued", 0, 0, null, null, DateTimeOffset.UtcNow.AddHours(1));
        public PhysicalFileDownloadResult? File;
    }

    private readonly object gate = new();
    private readonly ConcurrentDictionary<Guid, Job> jobs = new();
    private readonly Channel<Job> queue = Channel.CreateBounded<Job>(new BoundedChannelOptions(8)
    {
        SingleReader = true, FullMode = BoundedChannelFullMode.Wait
    });

    public ArchiveJobStatus Enqueue(string owner, int[]? ids)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArchiveRequestException("Влез отново в администраторския профил.");
        if (ids is not null && (ids.Length == 0 || ids.Any(id => id <= 0)))
            throw new ArchiveRequestException("Маркирай поне един валиден албум.");
        lock (gate)
        {
            // Returning the in-flight job also makes retries after a lost POST response safe.
            var pending = jobs.Values.FirstOrDefault(j => j.Owner == owner && !IsFinished(j.State.Status));
            if (pending is not null)
            {
                if ((pending.AlbumIds is null && ids is null) ||
                    (pending.AlbumIds is not null && ids is not null && pending.AlbumIds.Order().SequenceEqual(ids.Distinct().Order())))
                    return pending.State;
                throw new ArchiveRequestException("Вече се подготвя друг архив. Изчакай да завърши.");
            }
            if (jobs.Values.Count(j => j.Owner == owner && j.State.ExpiresAt > DateTimeOffset.UtcNow) >= 3)
                throw new ArchiveRequestException("Освободи готов архив с бутона „Затвори“, преди да създадеш нов.");
            var job = new Job(owner, ids?.Distinct().ToArray());
            jobs[job.State.Id] = job;
            if (!queue.Writer.TryWrite(job))
            {
                jobs.TryRemove(job.State.Id, out _);
                throw new ArchiveRequestException("В момента се подготвят други архиви. Опитай отново след малко.");
            }
            return job.State;
        }
    }

    public ArchiveJobStatus? Get(Guid id, string owner) =>
        jobs.TryGetValue(id, out var job) && job.Owner == owner && (!IsFinished(job.State.Status) || job.State.ExpiresAt > DateTimeOffset.UtcNow)
            ? Volatile.Read(ref job.State) : null;

    public FileDownloadResult? Open(Guid id, string owner)
    {
        lock (gate)
        {
            if (Get(id, owner)?.Status != "ready" || !jobs.TryGetValue(id, out var job) || job.File is null) return null;
            // Delete sharing allows expiry cleanup without interrupting an active download.
            var stream = new FileStream(job.File.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new(stream, job.File.ContentType, job.File.FileName);
        }
    }

    public async Task<bool> RemoveAsync(Guid id, string owner)
    {
        Job? job;
        lock (gate)
        {
            if (!jobs.TryGetValue(id, out job) || job.Owner != owner || !IsFinished(job.State.Status)) return false;
            jobs.TryRemove(id, out _);
        }
        if (job.File is not null) await CleanupAsync(job.File);
        return true;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(ProcessAsync(stoppingToken), ExpireAsync(stoppingToken));

    private async Task ProcessAsync(CancellationToken token)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(token))
        {
            try
            {
                Volatile.Write(ref job.State, job.State with { Status = "preparing", ExpiresAt = DateTimeOffset.UtcNow.AddHours(2) });
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromHours(2));
                await using var scope = scopes.CreateAsyncScope();
                var builder = scope.ServiceProvider.GetRequiredService<PortfolioArchiveBuilder>();
                job.File = await builder.BuildAsync(job.AlbumIds, progress =>
                    Volatile.Write(ref job.State, job.State with
                    { Status = progress.Status, CompletedFiles = progress.CompletedFiles, TotalFiles = progress.TotalFiles }), timeout.Token);
                Volatile.Write(ref job.State, job.State with
                { Status = "ready", FileName = job.File.FileName, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Archive job {JobId} failed", job.State.Id);
                Volatile.Write(ref job.State, job.State with
                {
                    Status = "failed", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                    Error = ex is ArchiveRequestException ? ex.Message : ex is OperationCanceledException
                        ? "Подготовката на архива беше прекъсната. Опитай отново."
                        : "Архивът не можа да бъде създаден. Провери файловете и опитай отново."
                });
            }
        }
    }

    private async Task ExpireAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(token))
            foreach (var job in jobs.Values.Where(j => IsFinished(j.State.Status) && j.State.ExpiresAt <= DateTimeOffset.UtcNow))
                await RemoveAsync(job.State.Id, job.Owner);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Writer.TryComplete();
        try { await base.StopAsync(cancellationToken); }
        finally
        {
            foreach (var job in jobs.Values)
                if (job.File is not null) await CleanupAsync(job.File);
        }
    }

    private async Task CleanupAsync(PhysicalFileDownloadResult file)
    {
        try { await file.CleanupAsync(); }
        catch (IOException ex) { logger.LogWarning(ex, "Unable to remove expired archive"); }
    }

    private static bool IsFinished(string status) => status is "ready" or "failed";
}
