using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/client-galleries")]
[Authorize]
public class ClientGalleriesController : ControllerBase
{
	private readonly IClientGalleryService _clientGalleryService;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly AppDbContext _dbContext;
	private readonly IFileStorageService _fileStorageService;

	public ClientGalleriesController(
		IClientGalleryService clientGalleryService,
		UserManager<ApplicationUser> userManager,
		AppDbContext dbContext,
		IFileStorageService fileStorageService)
	{
		_clientGalleryService = clientGalleryService;
		_userManager = userManager;
		_dbContext = dbContext;
		_fileStorageService = fileStorageService;
	}

	[HttpGet("my")]
	public async Task<IActionResult> GetMyGalleries()
	{
		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Unauthorized(new { message = "User not authenticated." });

		var galleries = await _clientGalleryService.GetMyGalleriesAsync(user.Id);
		return Ok(galleries);
	}

	[HttpGet("{galleryId:int}")]
	public async Task<IActionResult> GetGalleryDetails([FromRoute] int galleryId)
	{
		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Unauthorized(new { message = "User not authenticated." });

		var gallery = await _clientGalleryService.GetGalleryDetailsAsync(galleryId, user.Id);
		if (gallery == null)
			return NotFound(new { message = "Gallery not found or access denied." });

		return Ok(gallery);
	}

	[HttpGet("{galleryId:int}/photos/{photoId:int}/download")]
	public async Task<IActionResult> DownloadPhoto([FromRoute] int galleryId, [FromRoute] int photoId)
	{
		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Unauthorized(new { message = "User not authenticated." });

		var hasAccess = await _dbContext.UserAlbumAccesses
			.AsNoTracking()
			.AnyAsync(x =>
				x.PortfolioAlbumId == galleryId &&
				x.UserId == user.Id &&
				x.DownloadEnabled &&
				(!x.DownloadExpiresAtUtc.HasValue || x.DownloadExpiresAtUtc.Value >= DateTime.UtcNow));

		if (!hasAccess)
			return Forbid();

		var photo = await _dbContext.PortfolioImages
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == photoId && x.PortfolioAlbumId == galleryId && x.IsPublished);

		if (photo == null || string.IsNullOrWhiteSpace(photo.ImageUrl))
			return NotFound();

		var stream = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
		if (stream == null)
			return NotFound();

		var extension = Path.GetExtension(photo.ImageUrl);
		var fileName = $"photo-{photo.Id}{extension}";
		return File(stream, "application/octet-stream", fileName);
	}

	[HttpGet("{galleryId:int}/download")]
	public async Task<IActionResult> DownloadGalleryZip([FromRoute] int galleryId)
	{
		var user = await _userManager.GetUserAsync(User);
		if (user == null)
			return Unauthorized(new { message = "User not authenticated." });

		var hasAccess = await _dbContext.UserAlbumAccesses
			.AsNoTracking()
			.AnyAsync(x =>
				x.PortfolioAlbumId == galleryId &&
				x.UserId == user.Id &&
				x.DownloadEnabled &&
				(!x.DownloadExpiresAtUtc.HasValue || x.DownloadExpiresAtUtc.Value >= DateTime.UtcNow));

		if (!hasAccess)
			return Forbid();

		var album = await _dbContext.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.Images)
			.FirstOrDefaultAsync(x => x.Id == galleryId && x.AllowClientAccess);

		if (album == null)
			return NotFound();

		var photos = album.Images
			.Where(x => x.IsPublished && !string.IsNullOrWhiteSpace(x.ImageUrl))
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Id)
			.ToList();

		if (photos.Count == 0)
			return NotFound(new { message = "No downloadable photos found." });

		var memory = new MemoryStream();

		using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
		{
			foreach (var photo in photos)
			{
				var source = await _fileStorageService.OpenReadAsync(photo.ImageUrl);
				if (source == null)
					continue;

				await using (source)
				{
					var ext = Path.GetExtension(photo.ImageUrl);
					var entry = archive.CreateEntry($"{photo.DisplayOrder:D3}-{photo.Id}{ext}", CompressionLevel.Fastest);

					await using var entryStream = entry.Open();
					await source.CopyToAsync(entryStream);
				}
			}
		}

		memory.Position = 0;
		var safeName = string.Join("-", album.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
		if (string.IsNullOrWhiteSpace(safeName))
			safeName = $"gallery-{galleryId}";

		return File(memory, "application/zip", $"{safeName}.zip");
	}
}