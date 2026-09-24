using System.IO.Compression;
using System.Security.Claims;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ClientGalleryZipDownloadService(
    IClientGalleryService clientGalleryService,
    ClientGalleryEndpointUserContextService userContext,
    AppDbContext dbContext,
    IFileStorageService fileStorageService)
{
    public async Task<ControllerServiceResult> DownloadGalleryZipAsync(
        ClaimsPrincipal principal,
        int galleryId)
    {
        var user = await userContext.ResolveAsync(principal);
        if (user is null)
            return ClientGalleryEndpointUserContextService.Unauthenticated();

        var canDownload = principal.IsInRole("Admin") ||
            await clientGalleryService.UserCanAccessGalleryAsync(
                galleryId,
                user.Id,
                requireDownload: true);

        if (!canDownload)
            return ControllerServiceResult.Forbidden();

        var album = await dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x =>
                x.Id == galleryId &&
                x.AllowClientAccess &&
                !x.IsDeleted);

        if (album is null)
            return ControllerServiceResult.NotFound();

        var photos = album.Images
            .Where(x =>
                x.IsPublished &&
                !x.IsDeleted &&
                !string.IsNullOrWhiteSpace(x.ImageUrl))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToList();

        if (photos.Count == 0)
        {
            return ControllerServiceResult.NotFound(
                new { message = "No downloadable photos found." });
        }

        var memory = new MemoryStream();

        using (var archive = new ZipArchive(
            memory,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach (var photo in photos)
            {
                var source = await fileStorageService
                    .OpenReadAsync(photo.ImageUrl);

                if (source is null)
                    continue;

                await using (source)
                {
                    var extension = Path.GetExtension(photo.ImageUrl);
                    var entry = archive.CreateEntry(
                        $"{photo.DisplayOrder:D3}-{photo.Id}{extension}",
                        CompressionLevel.Fastest);

                    await using var entryStream = entry.Open();
                    await source.CopyToAsync(entryStream);
                }
            }
        }

        memory.Position = 0;

        return ControllerServiceResult.Ok(
            new FileDownloadResult(
                memory,
                "application/zip",
                $"{BuildSafeArchiveName(album.Title, galleryId)}.zip"));
    }

    public static string BuildSafeArchiveName(
        string title,
        int galleryId)
    {
        var safeName = string.Join(
                "-",
                (title ?? string.Empty).Split(
                    Path.GetInvalidFileNameChars(),
                    StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        return string.IsNullOrWhiteSpace(safeName)
            ? $"gallery-{galleryId}"
            : safeName;
    }
}
