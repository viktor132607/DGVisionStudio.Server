namespace DGVisionStudio.Domain.Entities;

public class EmailLog
{
    public Guid Id { get; set; }
    public Guid? ContactRequestId { get; set; }
    public ContactRequest? ContactRequest { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}
