using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryArchiveService(
    AppDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<AdminGalleryArchiveService> logger) : IAdminGalleryArchiveService
{
    public async Task<ControllerServiceResult> CreatePhysicalArchiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var download = await new PortfolioArchiveBuilder(dbContext, fileStorageService)
                .BuildAsync(null, null, cancellationToken);
            return ControllerServiceResult.Ok(download);
        }
        catch (ArchiveRequestException ex)
        {
            return ControllerServiceResult.NotFound(new { message = ex.Message });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build portfolio archive.");
            return ControllerServiceResult.Error(new { message = "Архивът не можа да бъде създаден. Опитай отново." });
        }
    }

    public async Task<ControllerServiceResult> PrepareStreamingArchiveAsync(CancellationToken cancellationToken)
    {
        // Validate the whole archive before sending ZIP headers, so a missing photo cannot produce a partial download.
        var result = await CreatePhysicalArchiveAsync(cancellationToken);
        if (result.Value is not PhysicalFileDownloadResult file) return result;
        return ControllerServiceResult.Ok(new StreamingFileDownloadResult(file.ContentType, file.FileName,
            async (destination, token) =>
            {
                try
                {
                    await using var source = File.OpenRead(file.Path);
                    await source.CopyToAsync(destination, token);
                }
                finally { await file.CleanupAsync(); }
            }));
    }
}
