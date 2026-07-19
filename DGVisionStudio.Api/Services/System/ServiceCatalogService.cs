using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly AppDbContext _context;

    public ServiceCatalogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetActiveAsync() =>
        ControllerServiceResult.Ok(await _context.Services
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetPublicByIdAsync(int id)
    {
        var item = await _context.Services.FindAsync(id);
        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await _context.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetAsync(int id)
    {
        var item = await _context.Services.FindAsync(id);
        return item is null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> CreateAsync(ServiceCardDto dto)
    {
        var validationError = Validate(dto);
        if (validationError is not null)
            return ControllerServiceResult.BadRequest(new { message = validationError });

        var maxOrder = await _context.Services.AnyAsync()
            ? await _context.Services.MaxAsync(x => x.DisplayOrder)
            : 0;

        var item = new Service
        {
            Title = dto.Title.Trim(),
            ShortDescription = Normalize(dto.ShortDescription),
            Description = dto.Description.Trim(),
            CoverImageUrl = Normalize(dto.CoverImageUrl),
            DisplayOrder = maxOrder + 1,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Services.Add(item);
        await _context.SaveChangesAsync();
        return new ControllerServiceResult(StatusCodes.Status201Created, item);
    }

    public async Task<ControllerServiceResult> UpdateAsync(int id, ServiceCardDto dto)
    {
        var item = await _context.Services.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        var validationError = Validate(dto);
        if (validationError is not null)
            return ControllerServiceResult.BadRequest(new { message = validationError });

        item.Title = dto.Title.Trim();
        item.ShortDescription = Normalize(dto.ShortDescription);
        item.Description = dto.Description.Trim();
        item.CoverImageUrl = Normalize(dto.CoverImageUrl);
        item.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> ReorderAsync(ReorderServicesDto dto)
    {
        if (dto.Ids.Count == 0)
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "Няма подадени услуги за пренареждане."
            });
        }

        var services = await _context.Services.ToListAsync();
        var byId = services.ToDictionary(x => x.Id);
        var order = 1;

        foreach (var id in dto.Ids.Distinct())
        {
            if (byId.TryGetValue(id, out var item))
                item.DisplayOrder = order++;
        }

        foreach (var item in services
            .Where(x => !dto.Ids.Contains(x.Id))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id))
        {
            item.DisplayOrder = order++;
        }

        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(await _context.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync());
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        var item = await _context.Services.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        _context.Services.Remove(item);
        await _context.SaveChangesAsync();
        await NormalizeDisplayOrderAsync();
        return ControllerServiceResult.NoContent();
    }

    private async Task NormalizeDisplayOrderAsync()
    {
        var items = await _context.Services
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
            items[i].DisplayOrder = i + 1;

        await _context.SaveChangesAsync();
    }

    private static string? Validate(ServiceCardDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return "Заглавието е задължително.";
        if (string.IsNullOrWhiteSpace(dto.Description))
            return "Описанието е задължително.";
        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
