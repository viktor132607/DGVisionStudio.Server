using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestService(AppDbContext context) : IAdminPrintRequestService
{
    public async Task<ControllerServiceResult> GetAllAsync()
    {
        var directRequests = await context.PrintRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioAlbum)
            .Include(x => x.Items)
                .ThenInclude(x => x.PortfolioImage)
            .Select(x => new PrintRequestDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UserEmail = x.User != null ? x.User.Email ?? string.Empty : string.Empty,
                PortfolioAlbumId = x.PortfolioAlbumId,
                AlbumTitle = x.PortfolioAlbum != null ? x.PortfolioAlbum.Title : string.Empty,
                FullName = x.FullName,
                Email = x.Email,
                Phone = x.Phone,
                Notes = x.Notes,
                Status = x.Status,
                IsSeenByAdmin = x.IsSeenByAdmin,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc,
                Items = x.Items.Select(item => new PrintRequestItemDto
                {
                    Id = item.Id,
                    PortfolioImageId = item.PortfolioImageId,
                    ImageUrl = item.PortfolioImage != null ? item.PortfolioImage.ImageUrl : string.Empty,
                    ThumbnailUrl = item.PortfolioImage != null ? item.PortfolioImage.ThumbnailUrl : null,
                    Quantity = item.Quantity,
                    Size = item.Size,
                    PaperType = item.PaperType,
                    Notes = item.Notes
                }).ToList()
            })
            .ToListAsync();

        var userUploadedAlbums = await context.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .Include(x => x.Images)
            .Where(x =>
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted)
            .Select(x => new PrintRequestDto
            {
                Id = -x.Id,
                UserId = x.OwnerUserId ?? string.Empty,
                UserEmail = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : string.Empty,
                PortfolioAlbumId = x.Id,
                AlbumTitle = x.Title,
                FullName = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : "Client upload",
                Email = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : string.Empty,
                Phone = null,
                Notes = x.Description,
                Status = MapClientPrintUploadStatus(x.UserGalleryStatus),
                IsSeenByAdmin = x.IsSeenByAdmin,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = null,
                Items = x.Images
                    .Where(image => !image.IsDeleted)
                    .OrderBy(image => image.DisplayOrder)
                    .ThenBy(image => image.Id)
                    .Select(image => new PrintRequestItemDto
                    {
                        Id = image.Id,
                        PortfolioImageId = image.Id,
                        ImageUrl = image.ImageUrl,
                        ThumbnailUrl = image.ThumbnailUrl,
                        Quantity = 1,
                        Size = string.Empty,
                        PaperType = null,
                        Notes = image.Caption
                    })
                    .ToList()
            })
            .ToListAsync();

        var result = directRequests
            .Concat(userUploadedAlbums)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        return ControllerServiceResult.Ok(result);
    }

    public async Task<ControllerServiceResult> GetByIdAsync(int id)
    {
        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums
                .AsNoTracking()
                .Include(x => x.OwnerUser)
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x =>
                    x.Id == albumId &&
                    x.GalleryType == GalleryType.ClientPrintUpload &&
                    x.IsUserUploaded &&
                    !x.IsDeleted);

            return album == null
                ? ControllerServiceResult.NotFound()
                : ControllerServiceResult.Ok(ToUserUploadedAlbumDto(album));
        }

        var request = await context.PrintRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioAlbum)
            .Include(x => x.Items)
                .ThenInclude(x => x.PortfolioImage)
            .FirstOrDefaultAsync(x => x.Id == id);

        return request == null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(ToPrintRequestDto(request));
    }

    public async Task<ControllerServiceResult> UpdateStatusAsync(int id, UpdatePrintRequestStatusDto dto)
    {
        var allowedStatuses = new[] { "New", "InProgress", "Completed", "Cancelled" };
        if (!allowedStatuses.Contains(dto.Status))
            return ControllerServiceResult.BadRequest("Invalid status.");

        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums.FirstOrDefaultAsync(x =>
                x.Id == albumId &&
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted);

            if (album == null)
                return ControllerServiceResult.NotFound();

            album.UserGalleryStatus = dto.Status switch
            {
                "InProgress" => UserClientGalleryStatus.PrintInProgress,
                "Completed" => UserClientGalleryStatus.Processed,
                "Cancelled" => UserClientGalleryStatus.Expired,
                _ => UserClientGalleryStatus.Pending
            };
            album.IsSeenByAdmin = true;
            await context.SaveChangesAsync();
            return ControllerServiceResult.NoContent();
        }

        var request = await context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
            return ControllerServiceResult.NotFound();

        request.Status = dto.Status;
        request.IsSeenByAdmin = true;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> MarkSeenAsync(int id)
    {
        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums.FirstOrDefaultAsync(x =>
                x.Id == albumId &&
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsDeleted);

            if (album == null)
                return ControllerServiceResult.NotFound();

            album.IsSeenByAdmin = true;
            await context.SaveChangesAsync();
            return ControllerServiceResult.NoContent();
        }

        var request = await context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
            return ControllerServiceResult.NotFound();

        request.IsSeenByAdmin = true;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> MarkAllSeenAsync()
    {
        var now = DateTime.UtcNow;
        var requests = await context.PrintRequests.Where(x => !x.IsSeenByAdmin).ToListAsync();
        foreach (var request in requests)
        {
            request.IsSeenByAdmin = true;
            request.UpdatedAtUtc = now;
        }

        var userUploadedAlbums = await context.PortfolioAlbums
            .Where(x =>
                x.GalleryType == GalleryType.ClientPrintUpload &&
                x.IsUserUploaded &&
                !x.IsSeenByAdmin &&
                !x.IsDeleted)
            .ToListAsync();

        foreach (var album in userUploadedAlbums)
            album.IsSeenByAdmin = true;

        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        if (id < 0)
        {
            var albumId = Math.Abs(id);
            var album = await context.PortfolioAlbums
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x =>
                    x.Id == albumId &&
                    x.GalleryType == GalleryType.ClientPrintUpload &&
                    x.IsUserUploaded &&
                    !x.IsDeleted);

            if (album == null)
                return ControllerServiceResult.NotFound();

            var now = DateTime.UtcNow;
            album.IsDeleted = true;
            album.DeletedAtUtc = now;
            album.IsPublished = false;
            album.AllowClientAccess = false;
            album.IsSeenByAdmin = true;
            album.UserGalleryStatus = UserClientGalleryStatus.Expired;

            foreach (var image in album.Images)
            {
                image.IsDeleted = true;
                image.DeletedAtUtc = now;
                image.IsPublished = false;
                image.IsCover = false;
            }

            await context.SaveChangesAsync();
            return ControllerServiceResult.NoContent();
        }

        var request = await context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
            return ControllerServiceResult.NotFound();

        context.PrintRequests.Remove(request);
        await context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }

    private static PrintRequestDto ToPrintRequestDto(PrintRequest request) => new()
    {
        Id = request.Id,
        UserId = request.UserId,
        UserEmail = request.User?.Email ?? string.Empty,
        PortfolioAlbumId = request.PortfolioAlbumId,
        AlbumTitle = request.PortfolioAlbum?.Title ?? string.Empty,
        FullName = request.FullName,
        Email = request.Email,
        Phone = request.Phone,
        Notes = request.Notes,
        Status = request.Status,
        IsSeenByAdmin = request.IsSeenByAdmin,
        CreatedAtUtc = request.CreatedAtUtc,
        UpdatedAtUtc = request.UpdatedAtUtc,
        Items = request.Items.Select(item => new PrintRequestItemDto
        {
            Id = item.Id,
            PortfolioImageId = item.PortfolioImageId,
            ImageUrl = item.PortfolioImage?.ImageUrl ?? string.Empty,
            ThumbnailUrl = item.PortfolioImage?.ThumbnailUrl,
            Quantity = item.Quantity,
            Size = item.Size,
            PaperType = item.PaperType,
            Notes = item.Notes
        }).ToList()
    };

    private static PrintRequestDto ToUserUploadedAlbumDto(PortfolioAlbum album) => new()
    {
        Id = -album.Id,
        UserId = album.OwnerUserId ?? string.Empty,
        UserEmail = album.OwnerUser?.Email ?? string.Empty,
        PortfolioAlbumId = album.Id,
        AlbumTitle = album.Title,
        FullName = album.OwnerUser?.Email ?? "Client upload",
        Email = album.OwnerUser?.Email ?? string.Empty,
        Phone = null,
        Notes = album.Description,
        Status = MapClientPrintUploadStatus(album.UserGalleryStatus),
        IsSeenByAdmin = album.IsSeenByAdmin,
        CreatedAtUtc = album.CreatedAtUtc,
        UpdatedAtUtc = null,
        Items = album.Images
            .Where(image => !image.IsDeleted)
            .OrderBy(image => image.DisplayOrder)
            .ThenBy(image => image.Id)
            .Select(image => new PrintRequestItemDto
            {
                Id = image.Id,
                PortfolioImageId = image.Id,
                ImageUrl = image.ImageUrl,
                ThumbnailUrl = image.ThumbnailUrl,
                Quantity = 1,
                Size = string.Empty,
                PaperType = null,
                Notes = image.Caption
            })
            .ToList()
    };

    private static string MapClientPrintUploadStatus(UserClientGalleryStatus status) => status switch
    {
        UserClientGalleryStatus.PrintInProgress => "InProgress",
        UserClientGalleryStatus.Processed => "Completed",
        UserClientGalleryStatus.Expired => "Cancelled",
        _ => "New"
    };
}
