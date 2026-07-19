using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/portfolio")]
public class AdminPortfolioController : ControllerBase
{
    private readonly IAdminPortfolioService _service;

    [ActivatorUtilitiesConstructor]
    public AdminPortfolioController(IAdminPortfolioService service)
    {
        _service = service;
    }

    public AdminPortfolioController(
        AppDbContext context,
        IAuditLogService auditLogService,
        ILogger<AdminPortfolioController> logger)
        : this(new AdminPortfolioService(
            context,
            auditLogService,
            NullLogger<AdminPortfolioService>.Instance))
    {
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() =>
        this.ToActionResult(await _service.GetCategoriesAsync());

    [HttpGet("categories/{id:int}")]
    public async Task<IActionResult> GetCategoryById(int id) =>
        this.ToActionResult(await _service.GetCategoryByIdAsync(id));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreatePortfolioCategoryRequest model) =>
        this.ToActionResult(await _service.CreateCategoryAsync(model, this.CreateAdminRequestContext()));

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdatePortfolioCategoryRequest model) =>
        this.ToActionResult(await _service.UpdateCategoryAsync(id, model, this.CreateAdminRequestContext()));

    [HttpPut("categories/{id:int}/move")]
    public async Task<IActionResult> MoveCategory(int id, [FromBody] MovePortfolioCategoryRequest model) =>
        this.ToActionResult(await _service.MoveCategoryAsync(id, model, this.CreateAdminRequestContext()));

    [HttpGet("categories/{id:int}/albums")]
    public async Task<IActionResult> GetCategoryAlbums(int id) =>
        this.ToActionResult(await _service.GetCategoryAlbumsAsync(id));

    [HttpPut("categories/{id:int}/albums")]
    public async Task<IActionResult> UpdateCategoryAlbums(int id, [FromBody] UpdateCategoryAlbumsRequest model) =>
        this.ToActionResult(await _service.UpdateCategoryAlbumsAsync(id, model, this.CreateAdminRequestContext()));

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id) =>
        this.ToActionResult(await _service.DeleteCategoryAsync(id, this.CreateAdminRequestContext()));

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums([FromQuery] PagedQueryDto query) =>
        this.ToActionResult(await _service.GetAlbumsAsync(query));

    [HttpPost("albums")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreatePortfolioAlbumRequest model) =>
        this.ToActionResult(await _service.CreateAlbumAsync(model, this.CreateAdminRequestContext()));

    [HttpPut("albums/{id:int}")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] UpdatePortfolioAlbumRequest model) =>
        this.ToActionResult(await _service.UpdateAlbumAsync(id, model, this.CreateAdminRequestContext()));

    [HttpDelete("albums/{id:int}")]
    public async Task<IActionResult> DeleteAlbum(int id) =>
        this.ToActionResult(await _service.DeleteAlbumAsync(id, this.CreateAdminRequestContext()));

    [HttpGet("images")]
    public async Task<IActionResult> GetImages([FromQuery] PagedQueryDto query) =>
        this.ToActionResult(await _service.GetImagesAsync(query));

    [HttpPost("images")]
    public async Task<IActionResult> CreateImage([FromBody] CreatePortfolioImageRequest model) =>
        this.ToActionResult(await _service.CreateImageAsync(model, this.CreateAdminRequestContext()));

    [HttpPut("images/{id:int}")]
    public async Task<IActionResult> UpdateImage(int id, [FromBody] UpdatePortfolioImageRequest model) =>
        this.ToActionResult(await _service.UpdateImageAsync(id, model, this.CreateAdminRequestContext()));

    [HttpDelete("images/{id:int}")]
    public async Task<IActionResult> DeleteImage(int id) =>
        this.ToActionResult(await _service.DeleteImageAsync(id, this.CreateAdminRequestContext()));
}
