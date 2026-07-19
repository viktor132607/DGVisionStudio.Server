using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class TestimonialService : ITestimonialService
{
    private readonly AppDbContext _context;

    public TestimonialService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetPublishedAsync() =>
        ControllerServiceResult.Ok(await _context.Testimonials
            .Where(x => x.IsPublished)
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync());

    public async Task<ControllerServiceResult> GetAllAsync() =>
        ControllerServiceResult.Ok(await _context.Testimonials
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync());

    public async Task<ControllerServiceResult> CreateAsync(Testimonial entity)
    {
        entity.CreatedAtUtc = DateTime.UtcNow;
        _context.Testimonials.Add(entity);
        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> UpdateAsync(int id, Testimonial model)
    {
        var entity = await _context.Testimonials.FindAsync(id);
        if (entity is null)
            return ControllerServiceResult.NotFound();

        entity.ClientName = model.ClientName;
        entity.ClientCompany = model.ClientCompany;
        entity.ClientRole = model.ClientRole;
        entity.Content = model.Content;
        entity.Rating = model.Rating;
        entity.IsPublished = model.IsPublished;
        entity.DisplayOrder = model.DisplayOrder;
        await _context.SaveChangesAsync();
        return ControllerServiceResult.Ok(entity);
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        var entity = await _context.Testimonials.FindAsync(id);
        if (entity is null)
            return ControllerServiceResult.NotFound();

        _context.Testimonials.Remove(entity);
        await _context.SaveChangesAsync();
        return ControllerServiceResult.NoContent();
    }
}
