using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryVideoUploadService(
    IAuditLogService auditLogService,
    ILogger<AdminGalleryMediaUploadService> logger,
    AppDbContext dbContext,
    AdminGalleryVideoFileStorageService fileStorage,
    ClientGalleryMapper mapper)
{
    public async Task<ControllerServiceResult> UploadAsync(
        int galleryId,
        IFormFile file,
        AdminRequestContext context)
    {
        if (galleryId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery id." });

        if (file is null || file.Length == 0)
            return ControllerServiceResult.BadRequest(new { message = "File is required." });

        if (file.Length > VideoUploadValidation.MaxFileSizeBytes)
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = $"Video is too large. Maximum size is {VideoUploadValidation.MaxFileSizeLabel}."
            });
        }

        if (!await VideoUploadValidation.IsAllowedAsync(file))
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "Only valid video files are allowed: mp4, mov, webm, m4v."
            });
        }

        var album = await dbContext.PortfolioAlbums
            .FirstOrDefaultAsync(x => x.Id == galleryId && !x.IsDeleted);
        if (album is null)
            return ControllerServiceResult.NotFound(new { message = "Gallery not found." });

        var savedPath = await fileStorage.SaveAsync(file);
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

        await auditLogService.LogAsync(
            context.UserId,
            context.Email,
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
            context.RemoteIpAddress,
            context.UserAgent,
            context.TraceId);

        return ControllerServiceResult.Ok(mapper.MapPhotoDto(video, true, galleryId));
    }
}
