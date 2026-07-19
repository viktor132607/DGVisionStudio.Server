using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPortfolioService : IAdminPortfolioService
{
    private readonly PortfolioCategoryAdminService _categories;
    private readonly PortfolioAlbumAdminService _albums;
    private readonly PortfolioImageAdminService _images;

    [ActivatorUtilitiesConstructor]
    public AdminPortfolioService(
        PortfolioCategoryAdminService categories,
        PortfolioAlbumAdminService albums,
        PortfolioImageAdminService images)
    {
        _categories = categories;
        _albums = albums;
        _images = images;
    }

    public AdminPortfolioService(
        AppDbContext context,
        IAuditLogService auditLogService,
        ILogger<AdminPortfolioService> logger)
        : this(
            new PortfolioCategoryAdminService(
                context,
                auditLogService,
                NullLogger<PortfolioCategoryAdminService>.Instance),
            new PortfolioAlbumAdminService(
                context,
                auditLogService,
                NullLogger<PortfolioAlbumAdminService>.Instance),
            new PortfolioImageAdminService(
                context,
                auditLogService,
                NullLogger<PortfolioImageAdminService>.Instance))
    {
    }

    public Task<ControllerServiceResult> GetCategoriesAsync() =>
        _categories.GetCategoriesAsync();

    public Task<ControllerServiceResult> GetCategoryByIdAsync(int id) =>
        _categories.GetCategoryByIdAsync(id);

    public Task<ControllerServiceResult> CreateCategoryAsync(
        CreatePortfolioCategoryRequest model,
        AdminRequestContext context) =>
        _categories.CreateCategoryAsync(model, context);

    public Task<ControllerServiceResult> UpdateCategoryAsync(
        int id,
        UpdatePortfolioCategoryRequest model,
        AdminRequestContext context) =>
        _categories.UpdateCategoryAsync(id, model, context);

    public Task<ControllerServiceResult> MoveCategoryAsync(
        int id,
        MovePortfolioCategoryRequest model,
        AdminRequestContext context) =>
        _categories.MoveCategoryAsync(id, model, context);

    public Task<ControllerServiceResult> GetCategoryAlbumsAsync(int id) =>
        _categories.GetCategoryAlbumsAsync(id);

    public Task<ControllerServiceResult> UpdateCategoryAlbumsAsync(
        int id,
        UpdateCategoryAlbumsRequest model,
        AdminRequestContext context) =>
        _categories.UpdateCategoryAlbumsAsync(id, model, context);

    public Task<ControllerServiceResult> DeleteCategoryAsync(
        int id,
        AdminRequestContext context) =>
        _categories.DeleteCategoryAsync(id, context);

    public Task<ControllerServiceResult> GetAlbumsAsync(PagedQueryDto query) =>
        _albums.GetAlbumsAsync(query);

    public Task<ControllerServiceResult> CreateAlbumAsync(
        CreatePortfolioAlbumRequest model,
        AdminRequestContext context) =>
        _albums.CreateAlbumAsync(model, context);

    public Task<ControllerServiceResult> UpdateAlbumAsync(
        int id,
        UpdatePortfolioAlbumRequest model,
        AdminRequestContext context) =>
        _albums.UpdateAlbumAsync(id, model, context);

    public Task<ControllerServiceResult> DeleteAlbumAsync(
        int id,
        AdminRequestContext context) =>
        _albums.DeleteAlbumAsync(id, context);

    public Task<ControllerServiceResult> GetImagesAsync(PagedQueryDto query) =>
        _images.GetImagesAsync(query);

    public Task<ControllerServiceResult> CreateImageAsync(
        CreatePortfolioImageRequest model,
        AdminRequestContext context) =>
        _images.CreateImageAsync(model, context);

    public Task<ControllerServiceResult> UpdateImageAsync(
        int id,
        UpdatePortfolioImageRequest model,
        AdminRequestContext context) =>
        _images.UpdateImageAsync(id, model, context);

    public Task<ControllerServiceResult> DeleteImageAsync(
        int id,
        AdminRequestContext context) =>
        _images.DeleteImageAsync(id, context);
}