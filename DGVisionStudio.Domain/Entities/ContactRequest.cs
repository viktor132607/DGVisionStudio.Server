using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Domain.Entities;

public class ContactRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.New;
    public string? AdminComment { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
