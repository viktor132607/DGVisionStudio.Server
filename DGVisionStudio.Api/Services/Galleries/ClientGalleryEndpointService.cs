using System.IO.Compression;
using System.Security.Claims;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ClientGalleryEndpointService : IClientGalleryEndpointService
{
    private readonly IClientGalleryService _clientGalleryService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;

    public ClientGalleryEndpointService(
        IClientGalleryService clientGalleryService,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IFileStorageService fileStorageService)
    {
        _clientGalleryService = clientGalleryService;
        _userManager = userManager;
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
    }

    public async Task<ControllerServiceResult> GetMyGalleriesAsync(ClaimsPrincipal principal)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();

        return ControllerServiceResult.Ok(await _clientGalleryService.GetMyGalleriesAsync(user.Id));
    }

    public async Task<ControllerServiceResult> CreateMyGalleryAsync(
        ClaimsPrincipal principal,
        CreateUserClientGalleryRequest request)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();
        if (string.IsNullOrWhiteSpace(request.Title))
            return ControllerServiceResult.BadRequest(new { message = "Title is required." });

        var galleryId = await _clientGalleryService.CreateUserGalleryAsync(user.Id, request);
        if (galleryId is null)
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "You can have up to 10 active galleries. Each gallery expires after 7 days."
            });
        }

        return ControllerServiceResult.Ok(new
        {
            message = "Gallery created successfully.",
            id = galleryId.Value
        });
    }

    public async Task<ControllerServiceResult> GetGalleryDetailsAsync(ClaimsPrincipal principal, int galleryId)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();

        var gallery = await _clientGalleryService.GetGalleryDetailsAsync(galleryId, user.Id);
        return gallery is null
            ? ControllerServiceResult.NotFound(new { message = "Gallery not found or access denied." })
            : ControllerServiceResult.Ok(gallery);
    }

    public async Task<ControllerServiceResult> UploadMyGalleryPhotoAsync(
        ClaimsPrincipal principal,
        int galleryId,
        IFormFile file)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();
        if (file is null || file.Length == 0)
            return ControllerServiceResult.BadRequest(new { message = "File is required." });

        var photo = await _clientGalleryService.UploadUserGalleryPhotoAsync(galleryId, user.Id, file);
        return photo is null
            ? ControllerServiceResult.BadRequest(new { message = "Gallery not found, expired, or access denied." })
            : ControllerServiceResult.Ok(photo);
    }

    public async Task<ControllerServiceResult> DeleteMyGalleryAsync(ClaimsPrincipal principal, int galleryId)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();

        var deleted = await _clientGalleryService.DeleteUserGalleryAsync(galleryId, user.Id);
        return deleted
            ? ControllerServiceResult.Ok(new { message = "Gallery deleted successfully." })
            : ControllerServiceResult.NotFound(new { message = "Gallery not found or access denied." });
    }

    public async Task<ControllerServiceResult> DownloadPhotoAsync(
        ClaimsPrincipal principal,
        int galleryId,
        int photoId)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();

        var result = await _clientGalleryService.OpenPhotoDownloadAsync(
            galleryId,
            photoId,
            user.Id,
            principal.IsInRole("Admin"));

        return result is null
            ? ControllerServiceResult.NotFound(new { message = "Photo not found or access denied." })
            : ControllerServiceResult.Ok(new FileDownloadResult(
                result.Value.Stream,
                result.Value.ContentType,
                result.Value.FileName));
    }

    public async Task<ControllerServiceResult> DownloadGalleryZipAsync(ClaimsPrincipal principal, int galleryId)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthenticated();

        var canDownload = principal.IsInRole("Admin") ||
            await _clientGalleryService.UserCanAccessGalleryAsync(galleryId, user.Id, requireDownload: true);

        if (!canDownload)
            return ControllerServiceResult.Forbidden();

        var album = await _dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == galleryId && x.AllowClientAccess && !x.IsDeleted);

        if (album is null)
            return ControllerServiceResult.NotFound();

        var photos = album.Images
            .Where(x => x.IsPublished && !x.IsDeleted && !string.IsNullOrWhiteSpace(x.ImageUrl))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToList();

        if (photos.Count == 0)
            return ControllerServiceResult.NotFound(new { message = "No downloadable photos found." });

        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            foreach (var photo in photos)
            {
                var source = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
                if (source is null)
                    continue;

                await using (source)
                {
                    var ext = Path.GetExtension(photo.ImageUrl);
                    var entry = archive.CreateEntry(
                        $"{photo.DisplayOrder:D3}-{photo.Id}{ext}",
                        CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await source.CopyToAsync(entryStream);
                }
            }
        }

        memory.Position = 0;
        var safeName = string.Join(
            "-",
            album.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = $"gallery-{galleryId}";

        return ControllerServiceResult.Ok(new FileDownloadResult(
            memory,
            "application/zip",
            $"{safeName}.zip"));
    }

    private static ControllerServiceResult Unauthenticated() =>
        ControllerServiceResult.Unauthorized(new { message = "User not authenticated." });
}
