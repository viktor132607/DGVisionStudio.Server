namespace DGVisionStudio.Api.Services;

internal sealed class PortfolioArchiveJobRuntime
{
    public PortfolioArchiveJobRuntime(
        IServiceScopeFactory scopes,
        ILogger<PortfolioArchiveJobs> logger)
    {
        Queue = new PortfolioArchiveJobQueue();
        var files =
            new PortfolioArchiveJobFileService(logger);
        Registry =
            new PortfolioArchiveJobRegistry(
                Queue,
                files);
        Processor =
            new PortfolioArchiveJobProcessor(
                Queue,
                scopes,
                logger);
    }

    public PortfolioArchiveJobQueue Queue { get; }
    public PortfolioArchiveJobRegistry Registry { get; }
    public PortfolioArchiveJobProcessor Processor { get; }
}
