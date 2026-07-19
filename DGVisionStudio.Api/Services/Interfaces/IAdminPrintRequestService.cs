using DGVisionStudio.Application.DTOs.PrintRequests;

namespace DGVisionStudio.Api.Services.Interfaces;

public interface IAdminPrintRequestService
{
    Task<ControllerServiceResult> GetAllAsync();
    Task<ControllerServiceResult> GetByIdAsync(int id);
    Task<ControllerServiceResult> UpdateStatusAsync(int id, UpdatePrintRequestStatusDto dto);
    Task<ControllerServiceResult> MarkSeenAsync(int id);
    Task<ControllerServiceResult> MarkAllSeenAsync();
    Task<ControllerServiceResult> DeleteAsync(int id);
}
