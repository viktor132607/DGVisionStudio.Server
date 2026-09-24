using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class PortfolioCategoryAuditService(IAuditLogService auditLogService)
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
            "PortfolioCategory",
            entityId,
            oldValue,
            newValue,
            requestContext.RemoteIpAddress,
            requestContext.UserAgent,
            requestContext.TraceId);
}
