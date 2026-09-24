using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryAdminService
{
    private readonly PortfolioCategoryQueryService _queries;
    private readonly PortfolioCategoryCommandService _commands;
    private readonly PortfolioCategoryOrderingService _ordering;
    private readonly PortfolioCategoryAlbumAssignmentService _albumAssignments;

    [ActivatorUtilitiesConstructor]
    public PortfolioCategoryAdminService(
        PortfolioCategoryQueryService queries,
        PortfolioCategoryCommandService commands,
        PortfolioCategoryOrderingService ordering,
        PortfolioCategoryAlbumAssignmentService albumAssignments)
    {
        _queries = queries;
        _commands = commands;
        _ordering = ordering;
        _albumAssignments = albumAssignments;
    }

    public PortfolioCategoryAdminService(
        AppDbContext context,
        IAuditLogService auditLogService,
        ILogger<PortfolioCategoryAdminService> logger)
    {
        _ = logger;
        var auditService = new PortfolioCategoryAuditService(auditLogService);
        var orderingService = new PortfolioCategoryOrderingService(
            context,
            auditService,
            NullLogger<PortfolioCategoryOrderingService>.Instance);

        _queries = new PortfolioCategoryQueryService(context);
        _commands = new PortfolioCategoryCommandService(
            context,
            auditService,
            orderingService,
            NullLogger<PortfolioCategoryCommandService>.Instance);
        _ordering = orderingService;
        _albumAssignments = new PortfolioCategoryAlbumAssignmentService(
            context,
            auditService,
            NullLogger<PortfolioCategoryAlbumAssignmentService>.Instance);
    }

    public Task<ControllerServiceResult> GetCategoriesAsync() =>
        _queries.GetCategoriesAsync();

    public Task<ControllerServiceResult> GetCategoryByIdAsync(int id) =>
        _queries.GetCategoryByIdAsync(id);

    public Task<ControllerServiceResult> CreateCategoryAsync(
        CreatePortfolioCategoryRequest model,
        AdminRequestContext requestContext) =>
        _commands.CreateCategoryAsync(model, requestContext);

    public Task<ControllerServiceResult> UpdateCategoryAsync(
        int id,
        UpdatePortfolioCategoryRequest model,
        AdminRequestContext requestContext) =>
        _commands.UpdateCategoryAsync(id, model, requestContext);

    public Task<ControllerServiceResult> MoveCategoryAsync(
        int id,
        MovePortfolioCategoryRequest model,
        AdminRequestContext requestContext) =>
        _ordering.MoveCategoryAsync(id, model, requestContext);

    public Task<ControllerServiceResult> GetCategoryAlbumsAsync(int id) =>
        _albumAssignments.GetCategoryAlbumsAsync(id);

    public Task<ControllerServiceResult> UpdateCategoryAlbumsAsync(
        int id,
        UpdateCategoryAlbumsRequest model,
        AdminRequestContext requestContext) =>
        _albumAssignments.UpdateCategoryAlbumsAsync(id, model, requestContext);

    public Task<ControllerServiceResult> DeleteCategoryAsync(
        int id,
        AdminRequestContext requestContext) =>
        _commands.DeleteCategoryAsync(id, requestContext);
}
