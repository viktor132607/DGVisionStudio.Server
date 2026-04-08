namespace DGVisionStudio.Domain.Entities;

public class PortfolioCategory
{
	public int Id { get; set; }
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string NameEn { get; set; } = string.Empty;
	public string? Description { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsActive { get; set; } = true;
	public ICollection<PortfolioAlbum> Albums { get; set; } = new List<PortfolioAlbum>();
}