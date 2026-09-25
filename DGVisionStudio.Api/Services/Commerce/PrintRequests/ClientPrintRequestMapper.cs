using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class ClientPrintRequestMapper
{
    public PrintRequestDto Map(PrintRequest request) =>
        new()
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

    public PrintRequest Create(
        string userId,
        CreatePrintRequestDto dto) =>
        new()
        {
            UserId = userId,
            PortfolioAlbumId = dto.PortfolioAlbumId,
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim(),
            Phone = dto.Phone?.Trim(),
            Notes = dto.Notes?.Trim(),
            Status = "New",
            IsSeenByAdmin = false,
            CreatedAtUtc = DateTime.UtcNow,
            Items = dto.Items.Select(item => new PrintRequestItem
            {
                PortfolioImageId = item.PortfolioImageId,
                Quantity = item.Quantity <= 0 ? 1 : item.Quantity,
                Size = item.Size.Trim(),
                PaperType = item.PaperType?.Trim(),
                Notes = item.Notes?.Trim()
            }).ToList()
        };
}
