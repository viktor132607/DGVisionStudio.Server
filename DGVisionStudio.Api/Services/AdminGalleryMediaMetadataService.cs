using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMediaMetadataService : IAdminGalleryMediaManagementService
{
    private readonly AppDbContext _context;

    public AdminGalleryMediaMetadataService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> UpdateMetadataAsync(
        int galleryId,
        int mediaId,
        UpdateGalleryMediaMetadataRequest request)
    {
        if (galleryId <= 0 || mediaId <= 0)
            return ControllerServiceResult.BadRequest(new { message = "Invalid gallery or media id." });

        var media = await _context.PortfolioImages
            .FirstOrDefaultAsync(x => x.Id == mediaId && x.PortfolioAlbumId == galleryId && !x.IsDeleted);
        if (media is null)
            return ControllerServiceResult.NotFound(new { message = "Media not found." });

        media.Name = Normalize(request.Name, 250);
        if (request.ClearAltAndCaption)
        {
            media.AltText = null;
            media.Caption = null;
        }

        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(new
        {
            media.Id,
            media.Name,
            media.AltText,
            media.Caption
        });
    }

    public Task<ControllerServiceResult> DownloadPhotoAsync(int galleryId, int photoId, AdminRequestContext context) => Unsupported();
    public Task<ControllerServiceResult> UploadPhotoAsync(int galleryId, IFormFile file, AdminRequestContext context) => Unsupported();
    public Task<ControllerServiceResult> UploadVideoAsync(int galleryId, IFormFile file, AdminRequestContext context) => Unsupported();
    public Task<ControllerServiceResult> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request, AdminRequestContext context) => Unsupported();
    public Task<ControllerServiceResult> DeletePhotoAsync(int galleryId, int photoId, AdminRequestContext context) => Unsupported();
    public Task<ControllerServiceResult> SetCoverImageAsync(int galleryId, SetGalleryCoverRequest request, AdminRequestContext context) => Unsupported();
    public Task<ControllerServiceResult> ReorderPhotosAsync(int galleryId, ReorderGalleryPhotosRequest request, AdminRequestContext context) => Unsupported();

    private static Task<ControllerServiceResult> Unsupported() =>
        Task.FromException<ControllerServiceResult>(new NotSupportedException());

    private static string? Normalize(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
