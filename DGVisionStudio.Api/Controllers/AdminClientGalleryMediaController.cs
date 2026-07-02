using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/client-galleries/{galleryId:int}/media")]
public class AdminClientGalleryMediaController : ControllerBase
{
	private readonly AppDbContext _context;

	public AdminClientGalleryMediaController(AppDbContext context)
	{
		_context = context;
	}

	[HttpPut("{mediaId:int}/metadata")]
	public async Task<IActionResult> UpdateMetadata(
		[FromRoute] int galleryId,
		[FromRoute] int mediaId,
		[FromBody] UpdateGalleryMediaMetadataRequest request)
	{
		if (galleryId <= 0 || mediaId <= 0)
			return BadRequest(new { message = "Invalid gallery or media id." });

		var media = await _context.PortfolioImages
			.FirstOrDefaultAsync(x => x.Id == mediaId && x.PortfolioAlbumId == galleryId && !x.IsDeleted);

		if (media == null)
			return NotFound(new { message = "Media not found." });

		media.Name = Normalize(request.Name, 250);
		media.AltText = Normalize(request.AltText, 500);
		media.Caption = Normalize(request.Caption, 1000);

		await _context.SaveChangesAsync();

		return Ok(new
		{
			media.Id,
			media.Name,
			media.AltText,
			media.Caption
		});
	}

	private static string? Normalize(string? value, int maxLength)
	{
		var trimmed = value?.Trim();
		if (string.IsNullOrWhiteSpace(trimmed)) return null;
		return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
	}
}

public class UpdateGalleryMediaMetadataRequest
{
	public string? Name { get; set; }
	public string? AltText { get; set; }
	public string? Caption { get; set; }
}
