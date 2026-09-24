using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioAlbumAuditService(IAuditLogService auditLogService)
{
    public Task LogAsync(
        string action,
        string? entityId,
        object? oldValue,
        object? newValue,
        AdminRequestContext requestContext) =>
        auditLogService.LogAsync(
            requestContext.UserId,
            requestContext.Email,
            action,
            "PortfolioAlbum",
            entityId,
            oldValue,
            newValue,
            requestContext.RemoteIpAddress,
            requestContext.UserAgent,
            requestContext.TraceId);
}
