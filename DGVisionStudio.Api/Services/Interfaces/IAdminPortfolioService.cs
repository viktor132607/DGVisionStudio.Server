using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Infrastructure.DTOs;

namespace DGVisionStudio.Api.Services.Interfaces;

public interface IAdminPortfolioService
{
    Task<ControllerServiceResult> GetCategoriesAsync();
    Task<ControllerServiceResult> GetCategoryByIdAsync(int id);
    Task<ControllerServiceResult> CreateCategoryAsync(CreatePortfolioCategoryRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> UpdateCategoryAsync(int id, UpdatePortfolioCategoryRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> MoveCategoryAsync(int id, MovePortfolioCategoryRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> GetCategoryAlbumsAsync(int id);
    Task<ControllerServiceResult> UpdateCategoryAlbumsAsync(int id, UpdateCategoryAlbumsRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> DeleteCategoryAsync(int id, AdminRequestContext context);
    Task<ControllerServiceResult> GetAlbumsAsync(PagedQueryDto query);
    Task<ControllerServiceResult> CreateAlbumAsync(CreatePortfolioAlbumRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> UpdateAlbumAsync(int id, UpdatePortfolioAlbumRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> DeleteAlbumAsync(int id, AdminRequestContext context);
    Task<ControllerServiceResult> GetImagesAsync(PagedQueryDto query);
    Task<ControllerServiceResult> CreateImageAsync(CreatePortfolioImageRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> UpdateImageAsync(int id, UpdatePortfolioImageRequest model, AdminRequestContext context);
    Task<ControllerServiceResult> DeleteImageAsync(int id, AdminRequestContext context);
}
