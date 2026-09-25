using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestCommandService
{
    private readonly AdminPrintRequestStatusService _status;
    private readonly AdminPrintRequestSeenService _seen;
    private readonly AdminPrintRequestDeletionService _deletion;

    [ActivatorUtilitiesConstructor]
    public AdminPrintRequestCommandService(
        AdminPrintRequestStatusService status,
        AdminPrintRequestSeenService seen,
        AdminPrintRequestDeletionService deletion)
    {
        _status = status;
        _seen = seen;
        _deletion = deletion;
    }

    public AdminPrintRequestCommandService(AppDbContext context)
        : this(
            new AdminPrintRequestStatusService(context),
            new AdminPrintRequestSeenService(context),
            new AdminPrintRequestDeletionService(context))
    {
    }

    public Task<ControllerServiceResult> UpdateStatusAsync(
        int id,
        UpdatePrintRequestStatusDto dto) =>
        _status.UpdateAsync(id, dto);

    public Task<ControllerServiceResult> MarkSeenAsync(int id) =>
        _seen.MarkSeenAsync(id);

    public Task<ControllerServiceResult> MarkAllSeenAsync() =>
        _seen.MarkAllSeenAsync();

    public Task<ControllerServiceResult> DeleteAsync(int id) =>
        _deletion.DeleteAsync(id);
}
