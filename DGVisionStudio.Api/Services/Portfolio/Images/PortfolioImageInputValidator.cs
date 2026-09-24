using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioImageInputValidator(AppDbContext context)
{
    public async Task<ControllerServiceResult?> ValidateAsync(
        PortfolioImageInput input)
    {
        var albumExists = await context.PortfolioAlbums
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == input.PortfolioAlbumId &&
                !x.IsUserUploaded);

        if (!albumExists)
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Невалиден албум." });
        }

        if (string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            return ControllerServiceResult.BadRequest(
                new { message = "ImageUrl е задължителен." });
        }

        return null;
    }
}
