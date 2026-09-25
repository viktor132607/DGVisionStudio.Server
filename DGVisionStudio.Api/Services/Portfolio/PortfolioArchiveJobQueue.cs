using System.Threading.Channels;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioArchiveJobQueue
{
    private readonly Channel<PortfolioArchiveJob> _queue =
        Channel.CreateBounded<PortfolioArchiveJob>(
            new BoundedChannelOptions(8)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

    internal bool TryWrite(PortfolioArchiveJob job) =>
        _queue.Writer.TryWrite(job);

    internal IAsyncEnumerable<PortfolioArchiveJob> ReadAllAsync(
        CancellationToken token) =>
        _queue.Reader.ReadAllAsync(token);

    internal void Complete() =>
        _queue.Writer.TryComplete();
}
