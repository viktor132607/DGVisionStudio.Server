using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminCalendarService : IAdminCalendarService
{
    private readonly AdminCalendarQueryService _queries;
    private readonly AdminCalendarContactRequestQueryService _contactRequests;
    private readonly AdminCalendarCommandService _commands;

    [ActivatorUtilitiesConstructor]
    public AdminCalendarService(
        AdminCalendarQueryService queries,
        AdminCalendarContactRequestQueryService contactRequests,
        AdminCalendarCommandService commands)
    {
        _queries = queries;
        _contactRequests = contactRequests;
        _commands = commands;
    }

    public AdminCalendarService(AppDbContext context)
        : this(
            new AdminCalendarQueryService(context),
            new AdminCalendarContactRequestQueryService(context),
            new AdminCalendarCommandService(
                context,
                new AdminCalendarEventInputService(context)))
    {
    }

    public Task<ControllerServiceResult> GetAllAsync() =>
        _queries.GetAllAsync();

    public Task<ControllerServiceResult> GetContactRequestsForImportAsync() =>
        _contactRequests.GetForImportAsync();

    public Task<ControllerServiceResult> GetContactRequestForImportAsync(Guid id) =>
        _contactRequests.GetForImportAsync(id);

    public Task<ControllerServiceResult> GetAsync(int id) =>
        _queries.GetAsync(id);

    public Task<ControllerServiceResult> CreateAsync(CalendarEventDto dto) =>
        _commands.CreateAsync(dto);

    public Task<ControllerServiceResult> UpdateAsync(int id, CalendarEventDto dto) =>
        _commands.UpdateAsync(id, dto);

    public Task<ControllerServiceResult> DeleteAsync(int id) =>
        _commands.DeleteAsync(id);
}
