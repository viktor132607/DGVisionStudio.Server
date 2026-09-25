using System.Security.Claims;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ClientPrintRequestQueryService(
    AppDbContext context,
    ClientPrintRequestUserContextService userContext,
    ClientPrintRequestMapper mapper)
{
    public async Task<ControllerServiceResult> GetMineAsync(
        ClaimsPrincipal principal)
    {
        var userId = userContext.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.Unauthorized(null);

        var requests = await context.PrintRequests
            .AsNoTracking()
            .Include(request => request.User)
            .Include(request => request.PortfolioAlbum)
            .Include(request => request.Items)
                .ThenInclude(item => item.PortfolioImage)
            .Where(request => request.UserId == userId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .ToListAsync();

        return ControllerServiceResult.Ok(
            requests.Select(mapper.Map).ToList());
    }

    public async Task<ControllerServiceResult> GetMineByIdAsync(
        ClaimsPrincipal principal,
        int id)
    {
        var userId = userContext.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
            return ControllerServiceResult.Unauthorized(null);

        var request = await context.PrintRequests
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.PortfolioAlbum)
            .Include(item => item.Items)
                .ThenInclude(item => item.PortfolioImage)
            .FirstOrDefaultAsync(item =>
                item.Id == id &&
                item.UserId == userId);

        return request is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(mapper.Map(request));
    }
}
