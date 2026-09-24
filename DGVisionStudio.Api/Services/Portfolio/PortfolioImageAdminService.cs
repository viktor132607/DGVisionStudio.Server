using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioImageAdminService
{
    private readonly PortfolioImageQueryService queries;
    private readonly PortfolioImageCommandService commands;

    [ActivatorUtilitiesConstructor]
    public PortfolioImageAdminService(
        PortfolioImageQueryService queries,
        PortfolioImageCommandService commands)
    {
        this.queries = queries;
        this.commands = commands;
    }

    public PortfolioImageAdminService(
        AppDbContext context,
        IAuditLogService auditLogService,
        ILogger<PortfolioImageAdminService> logger)
    {
        _ = logger;

        var mapper = new PortfolioImageMapper();
        var validator = new PortfolioImageInputValidator(context);
        var audit = new PortfolioImageAuditService(auditLogService);

        queries = new PortfolioImageQueryService(context);
        commands = new PortfolioImageCommandService(
            context,
            validator,
            mapper,
            audit,
            NullLogger<PortfolioImageCommandService>.Instance);
    }

    public Task<ControllerServiceResult> GetImagesAsync(PagedQueryDto query) =>
        queries.GetImagesAsync(query);

    public Task<ControllerServiceResult> CreateImageAsync(
        CreatePortfolioImageRequest model,
        AdminRequestContext requestContext) =>
        commands.CreateImageAsync(model, requestContext);

    public Task<ControllerServiceResult> UpdateImageAsync(
        int id,
        UpdatePortfolioImageRequest model,
        AdminRequestContext requestContext) =>
        commands.UpdateImageAsync(id, model, requestContext);

    public Task<ControllerServiceResult> DeleteImageAsync(
        int id,
        AdminRequestContext requestContext) =>
        commands.DeleteImageAsync(id, requestContext);
}
