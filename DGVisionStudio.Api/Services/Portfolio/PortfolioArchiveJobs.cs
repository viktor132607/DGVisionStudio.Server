using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed record ArchiveJobStatus(
    Guid Id,
    string Status,
    int CompletedFiles,
    int TotalFiles,
    string? FileName,
    string? Error,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Single disk-backed export worker.
/// Jobs survive HTTP timeouts and belong to their requesting admin.
/// </summary>
public sealed class PortfolioArchiveJobs :
    BackgroundService
{
    private readonly PortfolioArchiveJobQueue _queue;
    private readonly PortfolioArchiveJobRegistry _registry;
    private readonly PortfolioArchiveJobProcessor _processor;

    [ActivatorUtilitiesConstructor]
    public PortfolioArchiveJobs(
        PortfolioArchiveJobQueue queue,
        PortfolioArchiveJobRegistry registry,
        PortfolioArchiveJobProcessor processor)
    {
        _queue = queue;
        _registry = registry;
        _processor = processor;
    }

    public PortfolioArchiveJobs(
        IServiceScopeFactory scopes,
        ILogger<PortfolioArchiveJobs> logger)
        : this(
            new PortfolioArchiveJobRuntime(
                scopes,
                logger))
    {
    }

    private PortfolioArchiveJobs(
        PortfolioArchiveJobRuntime runtime)
        : this(
            runtime.Queue,
            runtime.Registry,
            runtime.Processor)
    {
    }

    public ArchiveJobStatus Enqueue(
        string owner,
        int[]? ids) =>
        _registry.Enqueue(owner, ids);

    public ArchiveJobStatus? Get(
        Guid id,
        string owner) =>
        _registry.Get(id, owner);

    public FileDownloadResult? Open(
        Guid id,
        string owner) =>
        _registry.Open(id, owner);

    public Task<bool> RemoveAsync(
        Guid id,
        string owner) =>
        _registry.RemoveAsync(id, owner);

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken) =>
        Task.WhenAll(
            _processor.ProcessAsync(stoppingToken),
            _registry.ExpireAsync(stoppingToken));

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        _queue.Complete();

        try
        {
            await base.StopAsync(cancellationToken);
        }
        finally
        {
            await _registry.CleanupAllAsync();
        }
    }
}
