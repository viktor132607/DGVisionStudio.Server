using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumAdminService
{
    private readonly PortfolioAlbumQueryService queries;
    private readonly PortfolioAlbumCommandService commands;

    [ActivatorUtilitiesConstructor]
    public PortfolioAlbumAdminService(
        PortfolioAlbumQueryService queries,
        PortfolioAlbumCommandService commands)
    {
        this.queries = queries;
        this.commands = commands;
    }

    public PortfolioAlbumAdminService(
        AppDbContext context,
        IAuditLogService auditLogService,
        ILogger<PortfolioAlbumAdminService> logger)
    {
        _ = logger;

        var auditService = new PortfolioAlbumAuditService(auditLogService);
        var validator = new PortfolioAlbumInputValidator(context);
        var mapper = new PortfolioAlbumMapper();

        queries = new PortfolioAlbumQueryService(context);
        commands = new PortfolioAlbumCommandService(
            context,
            validator,
            mapper,
            auditService,
            NullLogger<PortfolioAlbumCommandService>.Instance);
    }

    public Task<ControllerServiceResult> GetAlbumsAsync(PagedQueryDto query) =>
        queries.GetAlbumsAsync(query);

    public Task<ControllerServiceResult> CreateAlbumAsync(
        CreatePortfolioAlbumRequest model,
        AdminRequestContext requestContext) =>
        commands.CreateAlbumAsync(model, requestContext);

    public Task<ControllerServiceResult> UpdateAlbumAsync(
        int id,
        UpdatePortfolioAlbumRequest model,
        AdminRequestContext requestContext) =>
        commands.UpdateAlbumAsync(id, model, requestContext);

    public Task<ControllerServiceResult> DeleteAlbumAsync(
        int id,
        AdminRequestContext requestContext) =>
        commands.DeleteAlbumAsync(id, requestContext);
}
