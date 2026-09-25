using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryMutationAuditService(
    IAuditLogService auditLogService)
{
    public Task WriteAsync(
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext context) =>
        auditLogService.LogAsync(
            context.UserId,
            context.Email,
            action,
            entityType,
            entityId,
            oldValue,
            newValue,
            context.RemoteIpAddress,
            context.UserAgent,
            context.TraceId);
}
