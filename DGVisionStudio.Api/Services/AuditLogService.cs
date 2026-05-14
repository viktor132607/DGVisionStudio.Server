using System.Text.Json;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace DGVisionStudio.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
	private readonly AppDbContext _context;
	private readonly ILogger<AuditLogService> _logger;

	public AuditLogService(
		AppDbContext context,
		ILogger<AuditLogService> logger)
	{
		_context = context;
		_logger = logger;
	}

	public async Task LogAsync(
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
		CancellationToken cancellationToken = default)
	{
		try
		{
			var log = new AuditLog
			{
				AdminUserId = adminUserId,
				AdminEmail = adminEmail,
				Action = action,
				EntityType = entityType,
				EntityId = entityId,
				OldValue = SerializeValue(oldValue),
				NewValue = SerializeValue(newValue),
				IpAddress = ipAddress,
				UserAgent = userAgent,
				TraceId = traceId,
				CreatedAtUtc = DateTime.UtcNow
			};

			_context.Set<AuditLog>().Add(log);
			await _context.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to write audit log. AdminUserId: {AdminUserId}, AdminEmail: {AdminEmail}, Action: {Action}, EntityType: {EntityType}, EntityId: {EntityId}",
				adminUserId,
				adminEmail,
				action,
				entityType,
				entityId);
		}
	}

	private static string? SerializeValue(object? value)
	{
		if (value == null)
			return null;

		if (value is string stringValue)
			return stringValue;

		return JsonSerializer.Serialize(value, new JsonSerializerOptions
		{
			WriteIndented = false
		});
	}
}