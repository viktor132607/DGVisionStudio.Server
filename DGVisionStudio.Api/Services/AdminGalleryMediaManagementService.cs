using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaManagementService : IAdminGalleryMediaManagementService
{
    private const long MaxPhotoUploadSizeBytes = 20 * 1024 * 1024;
    private const long MaxVideoUploadSizeBytes = 100 * 1024 * 1024;

    private readonly IClientGalleryService _clientGalleryService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminGalleryMediaManagementService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ClientGalleryMapper _mapper;

    public AdminGalleryMediaManagementService(
        IClientGalleryService clientGalleryService,
        IAuditLogService auditLogService,
        ILogger<AdminGalleryMediaManagementService> logger,
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        ClientGalleryMapper mapper)
    {
        _clientGalleryService = clientGalleryService;
        _auditLogService = auditLogService;
        _logger = logger;
        _dbContext = dbContext;
        _environment = environment;
        _mapper = mapper;
    }

    public async Task<ControllerServiceResult> UpdateMetadataAsync(
        int galleryId,
        int mediaId,
        UpdateGalleryMediaMetadataRequest request)
    {
        if (galleryId <= 0 || mediaId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or media id." });

        var media = await _dbContext.PortfolioImages
            .FirstOrDefaultAsync(x =>
                x.Id == mediaId &&
                x.PortfolioAlbumId == galleryId &&
                !x.IsDeleted);

        if (media is null)
            return ControllerServiceResult.NotFound(new { message = "Media not found." });

        media.Name = Normalize(request.Name, 250);
        if (request.ClearAltAndCaption)
        {
            media.AltText = null;
            media.Caption = null;
        }

        await _dbContext.SaveChangesAsync();
        return ControllerServiceResult.Ok(new
        {
            media.Id,
            media.Name,
            media.AltText,
            media.Caption
        });
    }

    public async Task<ControllerServiceResult> DownloadPhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or photo id." });

        var result = await _clientGalleryService.OpenPhotoDownloadAsync(
            galleryId,
            photoId,
            userId: string.Empty,
            isAdmin: true);

        if (result is null)
        {
            _logger.LogWarning(
                "Admin photo download failed. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
                galleryId,
                photoId,
                context.DisplayName,
                context.TraceId);
            return ControllerServiceResult.NotFound(new { message = "Photo not found." });
        }

        _logger.LogInformation(
            "Admin downloaded photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            result.Value.FileName,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "DownloadPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            null,
            new { GalleryId = galleryId, PhotoId = photoId, result.Value.FileName },
            context);

        return ControllerServiceResult.Ok(new FileDownloadResult(
            result.Value.Stream,
            result.Value.ContentType,
            result.Value.FileName));
    }

    public async Task<ControllerServiceResult> UploadPhotoAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (file is null || file.Length == 0)
            return ControllerServiceResult.BadRequest(new { message = "File is required." });
        if (file.Length > MaxPhotoUploadSizeBytes)
            return ControllerServiceResult.BadRequest(new { message = "Photo is too large. Maximum size is 20MB." });
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return ControllerServiceResult.BadRequest(new { message = "Only image files are allowed." });

        var photo = await _clientGalleryService.UploadPhotoAsync(galleryId, file);
        if (photo is null)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        _logger.LogInformation(
            "Admin uploaded gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photo.Id,
            file.FileName,
            file.Length,
            file.ContentType,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "UploadGalleryPhoto",
            "ClientGalleryPhoto",
            photo.Id.ToString(),
            null,
            new
            {
                GalleryId = galleryId,
                PhotoId = photo.Id,
                file.FileName,
                file.Length,
                file.ContentType
            },
            context);

        return ControllerServiceResult.Ok(photo);
    }

    public async Task<ControllerServiceResult> UploadVideoAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (file is null || file.Length == 0)
            return ControllerServiceResult.BadRequest(new { message = "File is required." });
        if (file.Length > MaxVideoUploadSizeBytes)
            return ControllerServiceResult.BadRequest(new { message = "Video is too large. Maximum size is 100MB." });
        if (!IsAllowedVideo(file))
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "Only video files are allowed: mp4, mov, webm, m4v."
            });
        }

        var album = await _dbContext.PortfolioAlbums
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);
        if (album is null)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var uploadFolder = GetVideoUploadFolder();
        Directory.CreateDirectory(uploadFolder);
        var fullPath = Path.Combine(uploadFolder, safeFileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var savedPath = $"/uploads/portfolio/videos/{safeFileName}";
        var nextDisplayOrder = await _dbContext.PortfolioImages
            .Where(x => x.PortfolioAlbumId == galleryId && !x.IsDeleted)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;
        var originalName = Path.GetFileNameWithoutExtension(file.FileName);

        var video = new PortfolioImage
        {
            PortfolioAlbumId = galleryId,
            ImageUrl = savedPath,
            ThumbnailUrl = null,
            AltText = string.IsNullOrWhiteSpace(originalName) ? null : originalName.Trim(),
            Caption = null,
            Width = 0,
            Height = 0,
            DisplayOrder = nextDisplayOrder + 1,
            IsCover = false,
            IsPublished = true,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        _dbContext.PortfolioImages.Add(video);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Admin uploaded gallery video locally. GalleryId: {GalleryId}, MediaId: {MediaId}, FileName: {FileName}, FileSize: {FileSize}, ContentType: {ContentType}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            video.Id,
            file.FileName,
            file.Length,
            file.ContentType,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "UploadGalleryVideo",
            "ClientGalleryPhoto",
            video.Id.ToString(),
            null,
            new
            {
                GalleryId = galleryId,
                MediaId = video.Id,
                file.FileName,
                file.Length,
                file.ContentType,
                SavedPath = savedPath
            },
            context);

        return ControllerServiceResult.Ok(_mapper.MapPhotoDto(video, true, galleryId));
    }

    public async Task<ControllerServiceResult> UpdatePhotoAsync(
        int galleryId,
        int photoId,
        UpdateClientPhotoRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or photo id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });

        var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
        var oldPhoto = oldGallery?.Photos.FirstOrDefault(x => x.Id == photoId);
        var photo = await _clientGalleryService.UpdatePhotoAsync(galleryId, photoId, request);
        if (photo is null)
            return ControllerServiceResult.NotFound(new { message = "Photo not found." });

        _logger.LogInformation(
            "Admin updated gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, IsPublished: {IsPublished}, IsCover: {IsCover}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            request.IsPublished,
            request.IsCover,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "UpdateGalleryPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            oldPhoto,
            request,
            context);

        return ControllerServiceResult.Ok(photo);
    }

    public async Task<ControllerServiceResult> DeletePhotoAsync(
        int galleryId,
        int photoId,
        AdminRequestContext context)
    {
        if (galleryId <= 0 || photoId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or photo id." });

        var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
        var oldPhoto = oldGallery?.Photos.FirstOrDefault(x => x.Id == photoId);
        var deleted = await _clientGalleryService.DeletePhotoAsync(galleryId, photoId);
        if (!deleted)
            return ControllerServiceResult.NotFound(new { message = "Photo not found." });

        _logger.LogWarning(
            "Admin deleted gallery photo. GalleryId: {GalleryId}, PhotoId: {PhotoId}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            photoId,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "DeleteGalleryPhoto",
            "ClientGalleryPhoto",
            photoId.ToString(),
            oldPhoto,
            null,
            context);

        return ControllerServiceResult.Ok(new { message = "Photo deleted successfully." });
    }

    public async Task<ControllerServiceResult> SetCoverImageAsync(
        int galleryId,
        SetGalleryCoverRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });
        if (string.IsNullOrWhiteSpace(request.CoverImageUrl))
            return ControllerServiceResult.BadRequest(new { message = "Cover image url is required." });

        var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
        var updated = await _clientGalleryService.SetCoverImageAsync(galleryId, request.CoverImageUrl);
        if (!updated)
            return ControllerServiceResult.NotFound(new { message = "Gallery or photo not found." });

        _logger.LogInformation(
            "Admin changed gallery cover. GalleryId: {GalleryId}, CoverImageUrl: {CoverImageUrl}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.CoverImageUrl,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "SetGalleryCover",
            "ClientGallery",
            galleryId.ToString(),
            oldGallery,
            request,
            context);

        return ControllerServiceResult.Ok(new { message = "Cover image updated successfully." });
    }

    public async Task<ControllerServiceResult> ReorderPhotosAsync(
        int galleryId,
        ReorderGalleryPhotosRequest request,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });
        if (request is null)
            return ControllerServiceResult.BadRequest(new { message = "Request body is required." });
        if (request.OrderedPhotoIds is null || request.OrderedPhotoIds.Count == 0)
            return ControllerServiceResult.BadRequest(new { message = "Ordered photo ids are required." });

        var oldGallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
        var updated = await _clientGalleryService.ReorderPhotosAsync(galleryId, request.OrderedPhotoIds);
        if (!updated)
            return ControllerServiceResult.NotFound(new { message = "Gallery photos not found." });

        _logger.LogInformation(
            "Admin reordered gallery photos. GalleryId: {GalleryId}, PhotoCount: {PhotoCount}, Admin: {Admin}, TraceId: {TraceId}",
            galleryId,
            request.OrderedPhotoIds.Count,
            context.DisplayName,
            context.TraceId);

        await AuditAsync(
            "ReorderGalleryPhotos",
            "ClientGallery",
            galleryId.ToString(),
            oldGallery?.Photos.Select(x => new { x.Id, x.DisplayOrder }),
            request,
            context);

        return ControllerServiceResult.Ok(new { message = "Photos reordered successfully." });
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool IsAllowedVideo(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".mov",
            ".webm",
            ".m4v"
        };

        return allowedExtensions.Contains(extension) &&
            file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private string GetVideoUploadFolder()
    {
        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");

        return Path.Combine(webRootPath, "uploads", "portfolio", "videos");
    }

    private Task AuditAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext context) =>
        _auditLogService.LogAsync(
            context.UserId,
            context.Email,
            action,
            entityType,
            entityId,
            oldValue,
            newValue,
            context.RemoteIpAddress,
            context.UserAgent,
            context.TraceId);
}
