namespace DGVisionStudio.Domain.Entities;

public class AuditLog
{
	public int Id { get; set; }

	public string AdminUserId { get; set; } = string.Empty;

	public string AdminEmail { get; set; } = string.Empty;

	public string Action { get; set; } = string.Empty;

	public string EntityType { get; set; } = string.Empty;

	public string? EntityId { get; set; }

	public string? OldValue { get; set; }

	public string? NewValue { get; set; }

	public string? IpAddress { get; set; }

	public string? UserAgent { get; set; }

	public string? TraceId { get; set; }

	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}