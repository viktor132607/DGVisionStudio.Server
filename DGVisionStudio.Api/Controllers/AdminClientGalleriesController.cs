using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/admin/client-galleries")]
[Authorize(Roles = "Admin")]
public class AdminClientGalleriesController : ControllerBase
{
	private readonly IClientGalleryService _clientGalleryService;

	public AdminClientGalleriesController(IClientGalleryService clientGalleryService)
	{
		_clientGalleryService = clientGalleryService;
	}

	[HttpGet]
	public async Task<IActionResult> GetAllGalleries()
	{
		var galleries = await _clientGalleryService.GetAllGalleriesAsync();
		return Ok(galleries);
	}

	[HttpGet("{galleryId:int}")]
	public async Task<IActionResult> GetGalleryById([FromRoute] int galleryId)
	{
		var gallery = await _clientGalleryService.GetGalleryByIdAsync(galleryId);
		if (gallery == null)
			return NotFound(new { message = "Gallery not found." });

		return Ok(gallery);
	}

	[HttpPost]
	public async Task<IActionResult> CreateGallery([FromBody] AdminCreateClientGalleryRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
			return BadRequest(new { message = "Title is required." });

		var galleryId = await _clientGalleryService.CreateGalleryAsync(request);

		return Ok(new
		{
			message = "Client gallery created successfully.",
			id = galleryId
		});
	}

	[HttpPut("{galleryId:int}")]
	public async Task<IActionResult> UpdateGallery(
		[FromRoute] int galleryId,
		[FromBody] AdminUpdateClientGalleryRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
			return BadRequest(new { message = "Title is required." });

		var updated = await _clientGalleryService.UpdateGalleryAsync(galleryId, request);
		if (!updated)
			return NotFound(new { message = "Gallery not found." });

		return Ok(new { message = "Client gallery updated successfully." });
	}

	[HttpDelete("{galleryId:int}")]
	public async Task<IActionResult> DeleteGallery([FromRoute] int galleryId)
	{
		var deleted = await _clientGalleryService.DeleteGalleryAsync(galleryId);
		if (!deleted)
			return NotFound(new { message = "Gallery not found." });

		return Ok(new { message = "Client gallery deleted successfully." });
	}

	[HttpGet("{galleryId:int}/access")]
	public async Task<IActionResult> GetGalleryAccesses([FromRoute] int galleryId)
	{
		var accesses = await _clientGalleryService.GetGalleryAccessesAsync(galleryId);
		return Ok(accesses);
	}

	[HttpPost("{galleryId:int}/access")]
	public async Task<IActionResult> GrantAccess(
		[FromRoute] int galleryId,
		[FromBody] GrantGalleryAccessRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.UserEmail))
			return BadRequest(new { message = "User email is required." });

		var granted = await _clientGalleryService.GrantAccessAsync(galleryId, request);
		if (!granted)
			return BadRequest(new { message = "Gallery or user was not found." });

		return Ok(new { message = "Gallery access updated successfully." });
	}

	[HttpPut("{galleryId:int}/access/{userId}")]
	public async Task<IActionResult> UpdateAccess(
		[FromRoute] int galleryId,
		[FromRoute] string userId,
		[FromBody] UpdateGalleryAccessRequest request)
	{
		if (string.IsNullOrWhiteSpace(userId))
			return BadRequest(new { message = "User id is required." });

		var updated = await _clientGalleryService.UpdateAccessAsync(galleryId, userId, request);
		if (!updated)
			return NotFound(new { message = "Gallery access not found." });

		return Ok(new { message = "Gallery access updated successfully." });
	}

	[HttpDelete("{galleryId:int}/access/{userId}")]
	public async Task<IActionResult> RemoveAccess(
		[FromRoute] int galleryId,
		[FromRoute] string userId)
	{
		if (string.IsNullOrWhiteSpace(userId))
			return BadRequest(new { message = "User id is required." });

		var removed = await _clientGalleryService.RemoveAccessAsync(galleryId, userId);
		if (!removed)
			return NotFound(new { message = "Gallery access not found." });

		return Ok(new { message = "Gallery access removed successfully." });
	}

	[HttpPost("{galleryId:int}/photos/upload")]
	public async Task<IActionResult> UploadPhoto(
		[FromRoute] int galleryId,
		[FromForm] IFormFile file)
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "File is required." });

		var photo = await _clientGalleryService.UploadPhotoAsync(galleryId, file);
		if (photo == null)
			return NotFound(new { message = "Gallery not found." });

		return Ok(photo);
	}

	[HttpPut("{galleryId:int}/photos/{photoId:int}")]
	public async Task<IActionResult> UpdatePhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId,
		[FromBody] UpdateClientPhotoRequest request)
	{
		var photo = await _clientGalleryService.UpdatePhotoAsync(galleryId, photoId, request);
		if (photo == null)
			return NotFound(new { message = "Photo not found." });

		return Ok(photo);
	}

	[HttpDelete("{galleryId:int}/photos/{photoId:int}")]
	public async Task<IActionResult> DeletePhoto(
		[FromRoute] int galleryId,
		[FromRoute] int photoId)
	{
		var deleted = await _clientGalleryService.DeletePhotoAsync(galleryId, photoId);
		if (!deleted)
			return NotFound(new { message = "Photo not found." });

		return Ok(new { message = "Photo deleted successfully." });
	}

	[HttpPut("{galleryId:int}/cover")]
	public async Task<IActionResult> SetCoverImage(
		[FromRoute] int galleryId,
		[FromBody] SetGalleryCoverRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.CoverImageUrl))
			return BadRequest(new { message = "Cover image url is required." });

		var updated = await _clientGalleryService.SetCoverImageAsync(galleryId, request.CoverImageUrl);
		if (!updated)
			return NotFound(new { message = "Gallery or photo not found." });

		return Ok(new { message = "Cover image updated successfully." });
	}

	[HttpPut("{galleryId:int}/photos/reorder")]
	public async Task<IActionResult> ReorderPhotos(
		[FromRoute] int galleryId,
		[FromBody] ReorderGalleryPhotosRequest request)
	{
		if (request.OrderedPhotoIds == null || request.OrderedPhotoIds.Count == 0)
			return BadRequest(new { message = "Ordered photo ids are required." });

		var updated = await _clientGalleryService.ReorderPhotosAsync(galleryId, request.OrderedPhotoIds);
		if (!updated)
			return NotFound(new { message = "Gallery photos not found." });

		return Ok(new { message = "Photos reordered successfully." });
	}
}