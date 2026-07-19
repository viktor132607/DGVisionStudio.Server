using System.Security.Claims;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ClientPrintRequestEndpointService : IClientPrintRequestEndpointService
{
    private readonly AppDbContext _context;

    public ClientPrintRequestEndpointService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetMineAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.Unauthorized(null);

        var requests = await _context.PrintRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioAlbum)
            .Include(x => x.Items)
                .ThenInclude(x => x.PortfolioImage)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync();

        return ControllerServiceResult.Ok(requests);
    }

    public async Task<ControllerServiceResult> GetMineByIdAsync(
        ClaimsPrincipal principal,
        int id)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.Unauthorized(null);

        var request = await _context.PrintRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioAlbum)
            .Include(x => x.Items)
                .ThenInclude(x => x.PortfolioImage)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        return request is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(ToDto(request));
    }

    public async Task<ControllerServiceResult> CreateAsync(
        ClaimsPrincipal principal,
        CreatePrintRequestDto dto)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.Unauthorized(null);
        if (dto.PortfolioAlbumId <= 0)
            return ControllerServiceResult.BadRequest("Invalid album.");
        if (dto.Items is null || dto.Items.Count == 0)
            return ControllerServiceResult.BadRequest("Select at least one photo.");

        var hasAccess = await _context.UserAlbumAccesses.AnyAsync(x =>
            x.UserId == userId &&
            x.PortfolioAlbumId == dto.PortfolioAlbumId &&
            x.PreviewEnabled);
        if (!hasAccess)
            return ControllerServiceResult.Forbidden();

        var imageIds = dto.Items
            .Select(x => x.PortfolioImageId)
            .Distinct()
            .ToList();
        var validImageIds = await _context.PortfolioImages
            .Where(x =>
                x.PortfolioAlbumId == dto.PortfolioAlbumId &&
                imageIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (validImageIds.Count != imageIds.Count)
            return ControllerServiceResult.BadRequest("One or more selected photos are invalid.");

        var request = new PrintRequest
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
            Items = dto.Items.Select(x => new PrintRequestItem
            {
                PortfolioImageId = x.PortfolioImageId,
                Quantity = x.Quantity <= 0 ? 1 : x.Quantity,
                Size = x.Size.Trim(),
                PaperType = x.PaperType?.Trim(),
                Notes = x.Notes?.Trim()
            }).ToList()
        };

        _context.PrintRequests.Add(request);
        await _context.SaveChangesAsync();
        return new ControllerServiceResult(
            StatusCodes.Status201Created,
            new CreatedPrintRequestResult(request.Id));
    }

    private static PrintRequestDto ToDto(PrintRequest request) =>
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
}

public sealed record CreatedPrintRequestResult(int Id);
