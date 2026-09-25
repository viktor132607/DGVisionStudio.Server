using System.Collections.Concurrent;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioArchiveJob
{
    public PortfolioArchiveJob(
        string owner,
        int[]? albumIds)
    {
        Owner = owner;
        AlbumIds = albumIds;
        State = new(
            Guid.NewGuid(),
            "queued",
            0,
            0,
            null,
            null,
            DateTimeOffset.UtcNow.AddHours(1));
    }

    public string Owner { get; }
    public int[]? AlbumIds { get; }
    internal ArchiveJobStatus State;
    internal PhysicalFileDownloadResult? File;
}

public sealed class PortfolioArchiveJobRegistry(
    PortfolioArchiveJobQueue queue,
    PortfolioArchiveJobFileService files)
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<Guid, PortfolioArchiveJob> _jobs = new();

    public ArchiveJobStatus Enqueue(
        string owner,
        int[]? ids)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArchiveRequestException(
                "Влез отново в администраторския профил.");
        }

        if (ids is not null &&
            (ids.Length == 0 || ids.Any(id => id <= 0)))
        {
            throw new ArchiveRequestException(
                "Маркирай поне един валиден албум.");
        }

        lock (_gate)
        {
            var pending = _jobs.Values.FirstOrDefault(
                job =>
                    job.Owner == owner &&
                    !IsFinished(job.State.Status));

            if (pending is not null)
            {
                if ((pending.AlbumIds is null && ids is null) ||
                    (pending.AlbumIds is not null &&
                     ids is not null &&
                     pending.AlbumIds
                         .Order()
                         .SequenceEqual(
                             ids.Distinct().Order())))
                {
                    return pending.State;
                }

                throw new ArchiveRequestException(
                    "Вече се подготвя друг архив. Изчакай да завърши.");
            }

            if (_jobs.Values.Count(
                    job =>
                        job.Owner == owner &&
                        job.State.ExpiresAt >
                        DateTimeOffset.UtcNow) >= 3)
            {
                throw new ArchiveRequestException(
                    "Освободи готов архив с бутона „Затвори“, преди да създадеш нов.");
            }

            var job = new PortfolioArchiveJob(
                owner,
                ids?.Distinct().ToArray());

            _jobs[job.State.Id] = job;

            if (!queue.TryWrite(job))
            {
                _jobs.TryRemove(job.State.Id, out _);

                throw new ArchiveRequestException(
                    "В момента се подготвят други архиви. Опитай отново след малко.");
            }

            return job.State;
        }
    }

    public ArchiveJobStatus? Get(
        Guid id,
        string owner) =>
        _jobs.TryGetValue(id, out var job) &&
        job.Owner == owner &&
        (!IsFinished(job.State.Status) ||
         job.State.ExpiresAt > DateTimeOffset.UtcNow)
            ? Volatile.Read(ref job.State)
            : null;

    public FileDownloadResult? Open(
        Guid id,
        string owner)
    {
        lock (_gate)
        {
            if (Get(id, owner)?.Status != "ready" ||
                !_jobs.TryGetValue(id, out var job) ||
                job.File is null)
            {
                return null;
            }

            return files.Open(job.File);
        }
    }

    public async Task<bool> RemoveAsync(
        Guid id,
        string owner)
    {
        PortfolioArchiveJob? job;

        lock (_gate)
        {
            if (!_jobs.TryGetValue(id, out job) ||
                job.Owner != owner ||
                !IsFinished(job.State.Status))
            {
                return false;
            }

            _jobs.TryRemove(id, out _);
        }

        if (job.File is not null)
            await files.CleanupAsync(job.File);

        return true;
    }

    internal async Task ExpireAsync(
        CancellationToken token)
    {
        using var timer =
            new PeriodicTimer(
                TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(token))
        {
            foreach (var job in _jobs.Values.Where(
                         job =>
                             IsFinished(job.State.Status) &&
                             job.State.ExpiresAt <=
                             DateTimeOffset.UtcNow))
            {
                await RemoveAsync(
                    job.State.Id,
                    job.Owner);
            }
        }
    }

    internal async Task CleanupAllAsync()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.File is not null)
                await files.CleanupAsync(job.File);
        }
    }

    internal static bool IsFinished(string status) =>
        status is "ready" or "failed";
}
