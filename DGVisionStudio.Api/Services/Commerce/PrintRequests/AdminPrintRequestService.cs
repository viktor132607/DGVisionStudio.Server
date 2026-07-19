using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestService : IAdminPrintRequestService
{
    private readonly AdminPrintRequestQueryService _queries;
    private readonly AdminPrintRequestCommandService _commands;

    [ActivatorUtilitiesConstructor]
    public AdminPrintRequestService(
        AdminPrintRequestQueryService queries,
        AdminPrintRequestCommandService commands)
    {
        _queries = queries;
        _commands = commands;
    }

    public AdminPrintRequestService(AppDbContext context)
        : this(
            new AdminPrintRequestQueryService(context),
            new AdminPrintRequestCommandService(context))
    {
    }

    public Task<ControllerServiceResult> GetAllAsync() =>
        _queries.GetAllAsync();

    public Task<ControllerServiceResult> GetByIdAsync(int id) =>
        _queries.GetByIdAsync(id);

    public Task<ControllerServiceResult> UpdateStatusAsync(
        int id,
        UpdatePrintRequestStatusDto dto) =>
        _commands.UpdateStatusAsync(id, dto);

    public Task<ControllerServiceResult> MarkSeenAsync(int id) =>
        _commands.MarkSeenAsync(id);

    public Task<ControllerServiceResult> MarkAllSeenAsync() =>
        _commands.MarkAllSeenAsync();

    public Task<ControllerServiceResult> DeleteAsync(int id) =>
        _commands.DeleteAsync(id);
}