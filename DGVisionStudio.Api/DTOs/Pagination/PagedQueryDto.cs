namespace DGVisionStudio.Application.DTOs.Pagination;

public class PagedQueryDto
{
	private int _page = 1;
	private int _pageSize = 50;

	public int Page
	{
		get => _page;
		set => _page = value < 1 ? 1 : value;
	}

	public int PageSize
	{
		get => _pageSize;
		set => _pageSize = value is < 1 or > 200 ? 50 : value;
	}
}