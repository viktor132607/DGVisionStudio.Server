namespace DGVisionStudio.Application.Interfaces;

public interface IAuditLogService
{
	Task LogAsync(
		string adminUserId,
		string adminEmail,
		string action,
		string entityType,
		string? entityId,
		object? oldValue,
		object? newValue,
		string? ipAddress,
		string? userAgent,
		string? traceId,
		CancellationToken cancellationToken = default);
}