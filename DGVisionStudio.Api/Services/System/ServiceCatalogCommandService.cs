using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services;

public sealed class ServiceCatalogCommandService(
    AppDbContext context,
    ServiceCatalogInputService input,
    ServiceCatalogOrderingService ordering)
{
    public async Task<ControllerServiceResult> CreateAsync(
        ServiceCardDto dto)
    {
        var validationError = input.Validate(dto);
        if (validationError is not null)
        {
            return ControllerServiceResult.BadRequest(
                new { message = validationError });
        }

        var item = input.Create(
            dto,
            await ordering.GetNextDisplayOrderAsync(),
            DateTime.UtcNow);

        context.Services.Add(item);
        await context.SaveChangesAsync();

        return new ControllerServiceResult(
            StatusCodes.Status201Created,
            item);
    }

    public async Task<ControllerServiceResult> UpdateAsync(
        int id,
        ServiceCardDto dto)
    {
        var item = await context.Services.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        var validationError = input.Validate(dto);
        if (validationError is not null)
        {
            return ControllerServiceResult.BadRequest(
                new { message = validationError });
        }

        input.Apply(item, dto);
        await context.SaveChangesAsync();

        return ControllerServiceResult.Ok(item);
    }

    public async Task<ControllerServiceResult> ReorderAsync(
        ReorderServicesDto dto)
    {
        if (dto.Ids.Count == 0)
        {
            return ControllerServiceResult.BadRequest(new
            {
                message = "Няма подадени услуги за пренареждане."
            });
        }

        return ControllerServiceResult.Ok(
            await ordering.ReorderAsync(dto.Ids));
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        var item = await context.Services.FindAsync(id);
        if (item is null)
            return ControllerServiceResult.NotFound();

        context.Services.Remove(item);
        await context.SaveChangesAsync();
        await ordering.NormalizeDisplayOrderAsync();

        return ControllerServiceResult.NoContent();
    }
}
