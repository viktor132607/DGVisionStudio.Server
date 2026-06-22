namespace DGVisionStudio.Domain.Entities;

public class ShootingCalendarEvent
{
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? EventType { get; set; }
	public string? AssignedTo { get; set; }
	public string? ClientName { get; set; }
	public string? ClientPhone { get; set; }
	public string? Location { get; set; }
	public string? Description { get; set; }
	public string? Color { get; set; }
	public DateTime StartAtUtc { get; set; }
	public DateTime EndAtUtc { get; set; }
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAtUtc { get; set; }
}
