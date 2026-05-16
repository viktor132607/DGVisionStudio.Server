using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/print-requests")]
public class AdminPrintRequestsController : ControllerBase
{
	private readonly AppDbContext _context;

	public AdminPrintRequestsController(AppDbContext context)
	{
		_context = context;
	}

	[HttpGet]
	public async Task<ActionResult<List<PrintRequestDto>>> GetAll()
	{
		var directRequests = await _context.PrintRequests
			.AsNoTracking()
			.Include(x => x.User)
			.Include(x => x.PortfolioAlbum)
			.Include(x => x.Items)
				.ThenInclude(x => x.PortfolioImage)
			.Select(x => new PrintRequestDto
			{
				Id = x.Id,
				UserId = x.UserId,
				UserEmail = x.User != null ? x.User.Email ?? string.Empty : string.Empty,
				PortfolioAlbumId = x.PortfolioAlbumId,
				AlbumTitle = x.PortfolioAlbum != null ? x.PortfolioAlbum.Title : string.Empty,
				FullName = x.FullName,
				Email = x.Email,
				Phone = x.Phone,
				Notes = x.Notes,
				Status = x.Status,
				IsSeenByAdmin = x.IsSeenByAdmin,
				CreatedAtUtc = x.CreatedAtUtc,
				UpdatedAtUtc = x.UpdatedAtUtc,
				Items = x.Items.Select(item => new PrintRequestItemDto
				{
					Id = item.Id,
					PortfolioImageId = item.PortfolioImageId,
					ImageUrl = item.PortfolioImage != null ? item.PortfolioImage.ImageUrl : string.Empty,
					ThumbnailUrl = item.PortfolioImage != null ? item.PortfolioImage.ThumbnailUrl : null,
					Quantity = item.Quantity,
					Size = item.Size,
					PaperType = item.PaperType,
					Notes = item.Notes
				}).ToList()
			})
			.ToListAsync();

		var userUploadedAlbums = await _context.PortfolioAlbums
			.AsNoTracking()
			.Include(x => x.OwnerUser)
			.Include(x => x.Images)
			.Where(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				!x.IsDeleted)
			.Select(x => new PrintRequestDto
			{
				Id = -x.Id,
				UserId = x.OwnerUserId ?? string.Empty,
				UserEmail = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : string.Empty,
				PortfolioAlbumId = x.Id,
				AlbumTitle = x.Title,
				FullName = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : "Client upload",
				Email = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : string.Empty,
				Phone = null,
				Notes = x.Description,
				Status = MapClientPrintUploadStatus(x.UserGalleryStatus),
				IsSeenByAdmin = x.IsSeenByAdmin,
				CreatedAtUtc = x.CreatedAtUtc,
				UpdatedAtUtc = null,
				Items = x.Images
					.Where(image => !image.IsDeleted)
					.OrderBy(image => image.DisplayOrder)
					.ThenBy(image => image.Id)
					.Select(image => new PrintRequestItemDto
					{
						Id = image.Id,
						PortfolioImageId = image.Id,
						ImageUrl = image.ImageUrl,
						ThumbnailUrl = image.ThumbnailUrl,
						Quantity = 1,
						Size = string.Empty,
						PaperType = null,
						Notes = image.Caption
					})
					.ToList()
			})
			.ToListAsync();

		var result = directRequests
			.Concat(userUploadedAlbums)
			.OrderByDescending(x => x.CreatedAtUtc)
			.ToList();

		return Ok(result);
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<PrintRequestDto>> GetById(int id)
	{
		if (id < 0)
		{
			var albumId = Math.Abs(id);

			var album = await _context.PortfolioAlbums
				.AsNoTracking()
				.Include(x => x.OwnerUser)
				.Include(x => x.Images)
				.FirstOrDefaultAsync(x =>
					x.Id == albumId &&
					x.GalleryType == GalleryType.ClientPrintUpload &&
					x.IsUserUploaded &&
					!x.IsDeleted);

			if (album == null)
				return NotFound();

			return Ok(ToUserUploadedAlbumDto(album));
		}

		var request = await _context.PrintRequests
			.AsNoTracking()
			.Include(x => x.User)
			.Include(x => x.PortfolioAlbum)
			.Include(x => x.Items)
				.ThenInclude(x => x.PortfolioImage)
			.FirstOrDefaultAsync(x => x.Id == id);

		if (request == null)
			return NotFound();

		return Ok(ToPrintRequestDto(request));
	}

	[HttpPut("{id:int}/status")]
	public async Task<IActionResult> UpdateStatus(int id, UpdatePrintRequestStatusDto dto)
	{
		var allowedStatuses = new[] { "New", "InProgress", "Completed", "Cancelled" };

		if (!allowedStatuses.Contains(dto.Status))
			return BadRequest("Invalid status.");

		if (id < 0)
		{
			var albumId = Math.Abs(id);

			var album = await _context.PortfolioAlbums
				.FirstOrDefaultAsync(x =>
					x.Id == albumId &&
					x.GalleryType == GalleryType.ClientPrintUpload &&
					x.IsUserUploaded &&
					!x.IsDeleted);

			if (album == null)
				return NotFound();

			album.UserGalleryStatus = dto.Status switch
			{
				"InProgress" => UserClientGalleryStatus.PrintInProgress,
				"Completed" => UserClientGalleryStatus.Processed,
				"Cancelled" => UserClientGalleryStatus.Expired,
				_ => UserClientGalleryStatus.Pending
			};

			album.IsSeenByAdmin = true;

			await _context.SaveChangesAsync();

			return NoContent();
		}

		var request = await _context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);

		if (request == null)
			return NotFound();

		request.Status = dto.Status;
		request.IsSeenByAdmin = true;
		request.UpdatedAtUtc = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		return NoContent();
	}

	[HttpPut("{id:int}/seen")]
	public async Task<IActionResult> MarkSeen(int id)
	{
		if (id < 0)
		{
			var albumId = Math.Abs(id);

			var album = await _context.PortfolioAlbums
				.FirstOrDefaultAsync(x =>
					x.Id == albumId &&
					x.GalleryType == GalleryType.ClientPrintUpload &&
					x.IsUserUploaded &&
					!x.IsDeleted);

			if (album == null)
				return NotFound();

			album.IsSeenByAdmin = true;

			await _context.SaveChangesAsync();

			return NoContent();
		}

		var request = await _context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);

		if (request == null)
			return NotFound();

		request.IsSeenByAdmin = true;
		request.UpdatedAtUtc = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		return NoContent();
	}

	[HttpPut("seen")]
	public async Task<IActionResult> MarkAllSeen()
	{
		var now = DateTime.UtcNow;

		var requests = await _context.PrintRequests
			.Where(x => !x.IsSeenByAdmin)
			.ToListAsync();

		foreach (var request in requests)
		{
			request.IsSeenByAdmin = true;
			request.UpdatedAtUtc = now;
		}

		var userUploadedAlbums = await _context.PortfolioAlbums
			.Where(x =>
				x.GalleryType == GalleryType.ClientPrintUpload &&
				x.IsUserUploaded &&
				!x.IsSeenByAdmin &&
				!x.IsDeleted)
			.ToListAsync();

		foreach (var album in userUploadedAlbums)
		{
			album.IsSeenByAdmin = true;
		}

		await _context.SaveChangesAsync();

		return NoContent();
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> Delete(int id)
	{
		if (id < 0)
		{
			var albumId = Math.Abs(id);

			var album = await _context.PortfolioAlbums
				.Include(x => x.Images)
				.FirstOrDefaultAsync(x =>
					x.Id == albumId &&
					x.GalleryType == GalleryType.ClientPrintUpload &&
					x.IsUserUploaded &&
					!x.IsDeleted);

			if (album == null)
				return NotFound();

			var now = DateTime.UtcNow;

			album.IsDeleted = true;
			album.DeletedAtUtc = now;
			album.IsPublished = false;
			album.AllowClientAccess = false;
			album.IsSeenByAdmin = true;
			album.UserGalleryStatus = UserClientGalleryStatus.Expired;

			foreach (var image in album.Images)
			{
				image.IsDeleted = true;
				image.DeletedAtUtc = now;
				image.IsPublished = false;
				image.IsCover = false;
			}

			await _context.SaveChangesAsync();

			return NoContent();
		}

		var request = await _context.PrintRequests.FirstOrDefaultAsync(x => x.Id == id);

		if (request == null)
			return NotFound();

		_context.PrintRequests.Remove(request);
		await _context.SaveChangesAsync();

		return NoContent();
	}

	private static PrintRequestDto ToPrintRequestDto(PrintRequest request)
	{
		return new PrintRequestDto
		{
			Id = request.Id,
			UserId = request.UserId,
			UserEmail = request.User?.Email ?? string.Empty,
			PortfolioAlbumId = request.PortfolioAlbumId,
			AlbumTitle = request.PortfolioAlbum?.Title ?? string.Empty,
			FullName = request.FullName,
			Email = request.Email,
			Phone = request.Phone,
			Notes = request.Notes,
			Status = request.Status,
			IsSeenByAdmin = request.IsSeenByAdmin,
			CreatedAtUtc = request.CreatedAtUtc,
			UpdatedAtUtc = request.UpdatedAtUtc,
			Items = request.Items.Select(item => new PrintRequestItemDto
			{
				Id = item.Id,
				PortfolioImageId = item.PortfolioImageId,
				ImageUrl = item.PortfolioImage?.ImageUrl ?? string.Empty,
				ThumbnailUrl = item.PortfolioImage?.ThumbnailUrl,
				Quantity = item.Quantity,
				Size = item.Size,
				PaperType = item.PaperType,
				Notes = item.Notes
			}).ToList()
		};
	}

	private static PrintRequestDto ToUserUploadedAlbumDto(PortfolioAlbum album)
	{
		return new PrintRequestDto
		{
			Id = -album.Id,
			UserId = album.OwnerUserId ?? string.Empty,
			UserEmail = album.OwnerUser?.Email ?? string.Empty,
			PortfolioAlbumId = album.Id,
			AlbumTitle = album.Title,
			FullName = album.OwnerUser?.Email ?? "Client upload",
			Email = album.OwnerUser?.Email ?? string.Empty,
			Phone = null,
			Notes = album.Description,
			Status = MapClientPrintUploadStatus(album.UserGalleryStatus),
			IsSeenByAdmin = album.IsSeenByAdmin,
			CreatedAtUtc = album.CreatedAtUtc,
			UpdatedAtUtc = null,
			Items = album.Images
				.Where(image => !image.IsDeleted)
				.OrderBy(image => image.DisplayOrder)
				.ThenBy(image => image.Id)
				.Select(image => new PrintRequestItemDto
				{
					Id = image.Id,
					PortfolioImageId = image.Id,
					ImageUrl = image.ImageUrl,
					ThumbnailUrl = image.ThumbnailUrl,
					Quantity = 1,
					Size = string.Empty,
					PaperType = null,
					Notes = image.Caption
				})
				.ToList()
		};
	}

	private static string MapClientPrintUploadStatus(UserClientGalleryStatus status)
	{
		return status switch
		{
			UserClientGalleryStatus.PrintInProgress => "InProgress",
			UserClientGalleryStatus.Processed => "Completed",
			UserClientGalleryStatus.Expired => "Cancelled",
			_ => "New"
		};
	}
}