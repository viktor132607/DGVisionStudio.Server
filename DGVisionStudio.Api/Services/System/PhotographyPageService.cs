using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class PhotographyPageInput
{
    [Required(AllowEmptyStrings = true), MaxLength(150)] public string Slug { get; set; } = "";
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(200)] public string TitleEn { get; set; } = "";
    [MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(2000)] public string DescriptionEn { get; set; } = "";
    [MaxLength(30000)] public string Body { get; set; } = "";
    [MaxLength(30000)] public string BodyEn { get; set; } = "";
    [MaxLength(10000)] public string Preparation { get; set; } = "";
    [MaxLength(10000)] public string PreparationEn { get; set; } = "";
    public int? PortfolioCategoryId { get; set; }
    [Range(0, 10000)] public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PhotographyPageService(AppDbContext db)
{
    public async Task<List<PhotographyPage>> ListAsync(bool admin = false) => await db.PhotographyPages
        .AsNoTracking().Where(x => admin || x.IsActive)
        .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync();

    public async Task<ControllerServiceResult> SaveAsync(int? id, PhotographyPageInput input)
    {
        var page = id.HasValue ? await db.PhotographyPages.FindAsync(id.Value) : new PhotographyPage();
        if (page == null || page.IsDeleted) return ControllerServiceResult.NotFound();
        var overview = id.HasValue && page.Slug == "";
        var slug = (input.Slug ?? "").Trim().ToLowerInvariant();
        if (overview && (slug != "" || !input.IsActive))
            return ControllerServiceResult.BadRequest(new { message = "Общата страница трябва да остане активна на същия адрес." });
        if (!overview && !Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            return ControllerServiceResult.BadRequest(new { message = "Адресът трябва да съдържа латински малки букви, цифри и тирета." });
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), validation, true))
            return ControllerServiceResult.BadRequest(new { message = "Проверете задължителните полета и дължината на текста." });
        if (await db.PhotographyPages.AnyAsync(x => x.Slug == slug && x.Id != (id ?? 0)))
            return new ControllerServiceResult(409, new { message = "Вече има страница с този адрес." });
        if (input.PortfolioCategoryId.HasValue && !await db.PortfolioCategories.AnyAsync(x => x.Id == input.PortfolioCategoryId))
            return ControllerServiceResult.BadRequest(new { message = "Категорията не съществува." });
        page.Slug = slug;
        page.Title = input.Title.Trim(); page.TitleEn = (input.TitleEn ?? "").Trim();
        page.Description = input.Description ?? ""; page.DescriptionEn = input.DescriptionEn ?? "";
        page.Body = input.Body ?? ""; page.BodyEn = input.BodyEn ?? "";
        page.Preparation = input.Preparation ?? ""; page.PreparationEn = input.PreparationEn ?? "";
        page.PortfolioCategoryId = overview ? null : input.PortfolioCategoryId;
        page.DisplayOrder = input.DisplayOrder; page.IsActive = input.IsActive;
        if (!id.HasValue) db.PhotographyPages.Add(page);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return new ControllerServiceResult(409, new { message = "Вече има страница с този адрес." }); }
        return ControllerServiceResult.Ok(page);
    }

    public async Task<ControllerServiceResult> DeleteAsync(int id)
    {
        var page = await db.PhotographyPages.FirstOrDefaultAsync(x => x.Id == id);
        if (page == null) return ControllerServiceResult.NotFound();
        if (page.Slug == "") return ControllerServiceResult.BadRequest(new { message = "Общата страница може да се редактира, но не и да се изтрива." });
        page.IsDeleted = true;
        await db.SaveChangesAsync();
        return new ControllerServiceResult(204, null);
    }

    public async Task<ControllerServiceResult> AlbumsAsync(string slug)
    {
        var page = await db.PhotographyPages.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive);
        if (page == null) return ControllerServiceResult.NotFound();
        var now = DateTime.UtcNow;
        var albums = await db.PortfolioAlbums.AsNoTracking()
            .Where(x => page.PortfolioCategoryId != null && x.PortfolioCategoryId == page.PortfolioCategoryId
                && x.IsPublished && !x.IsUserUploaded && (x.PublishAtUtc == null || x.PublishAtUtc <= now)
                && x.PortfolioCategory != null && x.PortfolioCategory.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Take(6).Select(x => new {
                x.Id, x.Slug, x.Title, x.TitleEn,
                CoverImageUrl = x.CoverImageUrl ?? x.Images.Where(i => i.IsPublished)
                    .OrderByDescending(i => i.IsCover).ThenBy(i => i.DisplayOrder)
                    .Select(i => i.ThumbnailUrl ?? i.ImageUrl).FirstOrDefault()
            }).ToListAsync();
        return ControllerServiceResult.Ok(albums);
    }
}
