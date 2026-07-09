using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/portfolio/slideshow")]
public class PortfolioSlideshowController(IHomeSlideshowService slideshowService) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> GetSlideshowImages()
	{
		return Ok(await slideshowService.GetSlideshowImagesAsync());
	}

	[HttpGet("intro-video")]
	public async Task<IActionResult> GetIntroVideo()
	{
		return Ok(await slideshowService.GetIntroVideoAsync());
	}

	[HttpGet("settings")]
	public async Task<IActionResult> GetSettings()
	{
		return Ok(await slideshowService.GetSettingsAsync());
	}
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/slideshow")]
public class AdminSlideshowController(IHomeSlideshowService slideshowService) : ControllerBase
{
	private const long MaxIntroVideoUploadRequestSizeBytes = 105 * 1024 * 1024;

	[HttpGet]
	public async Task<IActionResult> GetSlideshowManagement()
	{
		return Ok(await slideshowService.GetManagementAsync());
	}

	[HttpPut]
	public async Task<IActionResult> UpdateSlideshow([FromBody] UpdateHomeSlideshowRequest model)
	{
		await slideshowService.UpdateAsync(model);
		return NoContent();
	}

	[HttpPost("video")]
	[RequestSizeLimit(MaxIntroVideoUploadRequestSizeBytes)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxIntroVideoUploadRequestSizeBytes)]
	public async Task<IActionResult> UploadIntroVideo([FromForm] IFormFile file)
	{
		try
		{
			return Ok(await slideshowService.UploadIntroVideoAsync(file));
		}
		catch (SlideshowValidationException ex)
		{
			return BadRequest(new { message = ex.Message });
		}
	}

	[HttpDelete("video")]
	public async Task<IActionResult> DeleteIntroVideo()
	{
		await slideshowService.DeleteIntroVideoAsync();
		return NoContent();
	}
}
