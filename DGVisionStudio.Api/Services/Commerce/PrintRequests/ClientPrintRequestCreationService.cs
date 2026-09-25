using System.Security.Claims;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ClientPrintRequestCreationService(
    AppDbContext context,
    ClientPrintRequestUserContextService userContext,
    ClientPrintRequestMapper mapper)
{
    public async Task<ControllerServiceResult> CreateAsync(
        ClaimsPrincipal principal,
        CreatePrintRequestDto dto)
    {
        var userId = userContext.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.Unauthorized(null);

        if (dto.PortfolioAlbumId <= 0)
            return ControllerServiceResult.BadRequest("Invalid album.");

        if (dto.Items is null || dto.Items.Count == 0)
        {
            return ControllerServiceResult.BadRequest(
                "Select at least one photo.");
        }

        var hasAccess = await context.UserAlbumAccesses.AnyAsync(access =>
            access.UserId == userId &&
            access.PortfolioAlbumId == dto.PortfolioAlbumId &&
            access.PreviewEnabled);

        if (!hasAccess)
            return ControllerServiceResult.Forbidden();

        var imageIds = dto.Items
            .Select(item => item.PortfolioImageId)
            .Distinct()
            .ToList();

        var validImageIds = await context.PortfolioImages
            .Where(image =>
                image.PortfolioAlbumId == dto.PortfolioAlbumId &&
                imageIds.Contains(image.Id))
            .Select(image => image.Id)
            .ToListAsync();

        if (validImageIds.Count != imageIds.Count)
        {
            return ControllerServiceResult.BadRequest(
                "One or more selected photos are invalid.");
        }

        var request = mapper.Create(userId, dto);

        context.PrintRequests.Add(request);
        await context.SaveChangesAsync();

        return new ControllerServiceResult(
            StatusCodes.Status201Created,
            new CreatedPrintRequestResult(request.Id));
    }
}
