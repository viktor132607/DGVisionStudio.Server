using System.Security.Claims;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/client/print-requests")]
public class ClientPrintRequestsController : ControllerBase
{
	private readonly AppDbContext _context;

	public ClientPrintRequestsController(AppDbContext context)
	{
		_context = context;
	}

	[HttpGet]
	public async Task<ActionResult<List<PrintRequestDto>>> GetMine()
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

		if (string.IsNullOrWhiteSpace(userId))
		{
			return Unauthorized();
		}

		var requests = await _context.PrintRequests
			.AsNoTracking()
			.Include(x => x.User)
			.Include(x => x.PortfolioAlbum)
			.Include(x => x.Items)
				.ThenInclude(x => x.PortfolioImage)
			.Where(x => x.UserId == userId)
			.OrderByDescending(x => x.CreatedAtUtc)
			.Select(x => ToDto(x))
			.ToListAsync();

		return Ok(requests);
	}

	[HttpGet("{id:int}")]
	public async Task<ActionResult<PrintRequestDto>> GetMineById(int id)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

		if (string.IsNullOrWhiteSpace(userId))
		{
			return Unauthorized();
		}

		var request = await _context.PrintRequests
			.AsNoTracking()
			.Include(x => x.User)
			.Include(x => x.PortfolioAlbum)
			.Include(x => x.Items)
				.ThenInclude(x => x.PortfolioImage)
			.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

		if (request == null)
		{
			return NotFound();
		}

		return Ok(ToDto(request));
	}

	[HttpPost]
	public async Task<IActionResult> Create(CreatePrintRequestDto dto)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

		if (string.IsNullOrWhiteSpace(userId))
		{
			return Unauthorized();
		}

		if (dto.PortfolioAlbumId <= 0)
		{
			return BadRequest("Invalid album.");
		}

		if (dto.Items == null || dto.Items.Count == 0)
		{
			return BadRequest("Select at least one photo.");
		}

		var hasAccess = await _context.UserAlbumAccesses
			.AnyAsync(x => x.UserId == userId && x.PortfolioAlbumId == dto.PortfolioAlbumId && x.PreviewEnabled);

		if (!hasAccess)
		{
			return Forbid();
		}

		var imageIds = dto.Items.Select(x => x.PortfolioImageId).Distinct().ToList();

		var validImageIds = await _context.PortfolioImages
			.Where(x => x.PortfolioAlbumId == dto.PortfolioAlbumId && imageIds.Contains(x.Id))
			.Select(x => x.Id)
			.ToListAsync();

		if (validImageIds.Count != imageIds.Count)
		{
			return BadRequest("One or more selected photos are invalid.");
		}

		var request = new PrintRequest
		{
			UserId = userId,
			PortfolioAlbumId = dto.PortfolioAlbumId,
			FullName = dto.FullName.Trim(),
			Email = dto.Email.Trim(),
			Phone = dto.Phone?.Trim(),
			Notes = dto.Notes?.Trim(),
			Status = "New",
			IsSeenByAdmin = false,
			CreatedAtUtc = DateTime.UtcNow,
			Items = dto.Items.Select(x => new PrintRequestItem
			{
				PortfolioImageId = x.PortfolioImageId,
				Quantity = x.Quantity <= 0 ? 1 : x.Quantity,
				Size = x.Size.Trim(),
				PaperType = x.PaperType?.Trim(),
				Notes = x.Notes?.Trim()
			}).ToList()
		};

		_context.PrintRequests.Add(request);
		await _context.SaveChangesAsync();

		return CreatedAtAction(nameof(GetMineById), new { id = request.Id }, new { request.Id });
	}

	private static PrintRequestDto ToDto(PrintRequest request)
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
}