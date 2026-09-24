using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumInputValidator(AppDbContext context)
{
    public async Task<ControllerServiceResult?> ValidateAsync(
        PortfolioAlbumInput input,
        int? excludedAlbumId = null)
    {
        if (!await context.PortfolioCategories
                .AsNoTracking()
                .AnyAsync(x => x.Id == input.PortfolioCategoryId))
        {
            return ControllerServiceResult.BadRequest(new { message = "Невалидна категория." });
        }

        if (string.IsNullOrWhiteSpace(input.Slug))
            return ControllerServiceResult.BadRequest(new { message = "Slug е задължителен." });

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Заглавието на български е задължително." });
        }

        var duplicateQuery = context.PortfolioAlbums
            .AsNoTracking()
            .Where(x => x.Slug == input.Slug);

        if (excludedAlbumId.HasValue)
            duplicateQuery = duplicateQuery.Where(x => x.Id != excludedAlbumId.Value);

        if (await duplicateQuery.AnyAsync())
        {
            return ControllerServiceResult.BadRequest(
                new { message = "Вече съществува албум със същия slug." });
        }

        return null;
    }
}
