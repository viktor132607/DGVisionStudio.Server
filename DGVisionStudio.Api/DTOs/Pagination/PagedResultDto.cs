namespace DGVisionStudio.Application.DTOs.Pagination;

public class PagedResultDto<T>
{
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int Total { get; set; }
	public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)Total / PageSize);
	public List<T> Items { get; set; } = new();
}