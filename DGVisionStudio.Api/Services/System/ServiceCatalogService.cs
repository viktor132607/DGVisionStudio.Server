using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly ServiceCatalogQueryService _queries;
    private readonly ServiceCatalogCommandService _commands;

    [ActivatorUtilitiesConstructor]
    public ServiceCatalogService(
        ServiceCatalogQueryService queries,
        ServiceCatalogCommandService commands)
    {
        _queries = queries;
        _commands = commands;
    }

    public ServiceCatalogService(AppDbContext context)
        : this(
            new ServiceCatalogQueryService(context),
            new ServiceCatalogCommandService(
                context,
                new ServiceCatalogInputService(),
                new ServiceCatalogOrderingService(context)))
    {
    }

    public Task<ControllerServiceResult> GetActiveAsync() =>
        _queries.GetActiveAsync();

    public Task<ControllerServiceResult> GetPublicByIdAsync(int id) =>
        _queries.GetPublicByIdAsync(id);

    public Task<ControllerServiceResult> GetAllAsync() =>
        _queries.GetAllAsync();

    public Task<ControllerServiceResult> GetAsync(int id) =>
        _queries.GetAsync(id);

    public Task<ControllerServiceResult> CreateAsync(ServiceCardDto dto) =>
        _commands.CreateAsync(dto);

    public Task<ControllerServiceResult> UpdateAsync(
        int id,
        ServiceCardDto dto) =>
        _commands.UpdateAsync(id, dto);

    public Task<ControllerServiceResult> ReorderAsync(
        ReorderServicesDto dto) =>
        _commands.ReorderAsync(dto);

    public Task<ControllerServiceResult> DeleteAsync(int id) =>
        _commands.DeleteAsync(id);
}
