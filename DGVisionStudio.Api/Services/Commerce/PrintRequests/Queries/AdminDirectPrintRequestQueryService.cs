using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminDirectPrintRequestQueryService(
    AppDbContext context,
    AdminPrintRequestMapper mapper)
{
    public async Task<List<PrintRequestDto>> GetAllAsync()
    {
        var requests = await context.PrintRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioAlbum)
            .Include(x => x.Items)
                .ThenInclude(x => x.PortfolioImage)
            .ToListAsync();

        return requests
            .Select(mapper.ToPrintRequestDto)
            .ToList();
    }

    public async Task<PrintRequestDto?> GetByIdAsync(int id)
    {
        var request = await context.PrintRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioAlbum)
            .Include(x => x.Items)
                .ThenInclude(x => x.PortfolioImage)
            .FirstOrDefaultAsync(x => x.Id == id);

        return request == null
            ? null
            : mapper.ToPrintRequestDto(request);
    }
}
