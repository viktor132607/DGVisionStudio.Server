namespace DGVisionStudio.Domain.Entities;

public class PhotographyPage
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string Description { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string Body { get; set; } = "";
    public string BodyEn { get; set; } = "";
    public string Preparation { get; set; } = "";
    public string PreparationEn { get; set; } = "";
    public int? PortfolioCategoryId { get; set; }
    public PortfolioCategory? PortfolioCategory { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}
