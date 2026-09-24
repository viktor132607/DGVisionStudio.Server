using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestMapper
{
    public PrintRequestDto ToPrintRequestDto(PrintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new PrintRequestDto
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
            Items = request.Items
                .Select(ToPrintRequestItemDto)
                .ToList()
        };
    }

    public PrintRequestDto ToUserUploadedAlbumDto(PortfolioAlbum album)
    {
        ArgumentNullException.ThrowIfNull(album);

        return new PrintRequestDto
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
                .Select(ToUploadedImageItemDto)
                .ToList()
        };
    }

    public string MapClientPrintUploadStatus(UserClientGalleryStatus status) =>
        status switch
        {
            UserClientGalleryStatus.PrintInProgress => "InProgress",
            UserClientGalleryStatus.Processed => "Completed",
            UserClientGalleryStatus.Expired => "Cancelled",
            _ => "New"
        };

    private static PrintRequestItemDto ToPrintRequestItemDto(
        PrintRequestItem item) =>
        new()
        {
            Id = item.Id,
            PortfolioImageId = item.PortfolioImageId,
            ImageUrl = item.PortfolioImage?.ImageUrl ?? string.Empty,
            ThumbnailUrl = item.PortfolioImage?.ThumbnailUrl,
            Quantity = item.Quantity,
            Size = item.Size,
            PaperType = item.PaperType,
            Notes = item.Notes
        };

    private static PrintRequestItemDto ToUploadedImageItemDto(
        PortfolioImage image) =>
        new()
        {
            Id = image.Id,
            PortfolioImageId = image.Id,
            ImageUrl = image.ImageUrl,
            ThumbnailUrl = image.ThumbnailUrl,
            Quantity = 1,
            Size = string.Empty,
            PaperType = null,
            Notes = image.Caption
        };
}
