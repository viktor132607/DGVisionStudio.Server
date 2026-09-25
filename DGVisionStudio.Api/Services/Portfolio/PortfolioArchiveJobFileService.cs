namespace DGVisionStudio.Api.Services;

public sealed class PortfolioArchiveJobFileService(
    ILogger<PortfolioArchiveJobs> logger)
{
    public FileDownloadResult Open(
        PhysicalFileDownloadResult file)
    {
        var stream = new FileStream(
            file.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            81920,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        return new(
            stream,
            file.ContentType,
            file.FileName);
    }

    public async Task CleanupAsync(
        PhysicalFileDownloadResult file)
    {
        try
        {
            await file.CleanupAsync();
        }
        catch (IOException ex)
        {
            logger.LogWarning(
                ex,
                "Unable to remove expired archive");
        }
    }
}
