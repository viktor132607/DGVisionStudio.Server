using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaUploadService(
    IClientGalleryService clientGalleryService,
    IAuditLogService auditLogService,
    ILogger<AdminGalleryMediaUploadService> logger,
    AppDbContext dbContext,
    IWebHostEnvironment environment,
    ClientGalleryMapper mapper)
{
    private const long MaxPhotoUploadSizeBytes = 20 * 1024 * 1024;
    private const long MaxVideoUploadSizeBytes = 100 * 1024 * 1024;

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

        var photo = await clientGalleryService.UploadPhotoAsync(galleryId, file);
        if (photo is null)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        logger.LogInformation(
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

        var album = await dbContext.PortfolioAlbums
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
        var nextDisplayOrder = await dbContext.PortfolioImages
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

        dbContext.PortfolioImages.Add(video);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
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

        return ControllerServiceResult.Ok(mapper.MapPhotoDto(video, true, galleryId));
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
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");

        return Path.Combine(webRootPath, "uploads", "portfolio", "videos");
    }

    private Task AuditAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext context) =>
        auditLogService.LogAsync(
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