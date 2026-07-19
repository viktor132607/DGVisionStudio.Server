using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/portfolio")]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioQueryService _service;

    [ActivatorUtilitiesConstructor]
    public PortfolioController(IPortfolioQueryService service)
    {
        _service = service;
    }

    public PortfolioController(AppDbContext context)
        : this(new PortfolioQueryService(context))
    {
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() =>
        this.ToActionResult(await _service.GetCategoriesAsync());

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums([FromQuery] int? categoryId = null) =>
        this.ToActionResult(await _service.GetAlbumsAsync(categoryId));

    [HttpGet("albums/{slug}")]
    public async Task<IActionResult> GetAlbum(string slug) =>
        this.ToActionResult(await _service.GetAlbumAsync(slug));

    [HttpGet("images")]
    public async Task<IActionResult> GetImages([FromQuery] int? albumId = null) =>
        this.ToActionResult(await _service.GetImagesAsync(albumId));
}
