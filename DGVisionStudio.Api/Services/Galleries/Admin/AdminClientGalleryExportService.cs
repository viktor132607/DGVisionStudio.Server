using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Api.Services;

public sealed class AdminClientGalleryExportService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<AdminClientGalleryExportService> logger)
{
    public async Task<ControllerServiceResult> DownloadAllAlbumsAsync(AdminRequestContext context)
    {
        try
        {
            var archive = await new PortfolioArchiveBuilder(dbContext, fileStorageService)
                .BuildAsync(null, null, CancellationToken.None);
            try
            {
                var stream = new FileStream(archive.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
                return ControllerServiceResult.Ok(new FileDownloadResult(stream, archive.ContentType, archive.FileName));
            }
            catch { await archive.CleanupAsync(); throw; }
        }
        catch (ArchiveRequestException ex) { return ControllerServiceResult.NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export albums. TraceId: {TraceId}", context.TraceId);
            return ControllerServiceResult.Error(new { message = "Архивът не можа да бъде създаден. Опитай отново." });
        }
    }
}
