namespace DGVisionStudio.Infrastructure.DTOs;

public class UpdatePortfolioCategoryRequest
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string NameEn { get; set; } = string.Empty;
	public string? Description { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsActive { get; set; } = true;
}