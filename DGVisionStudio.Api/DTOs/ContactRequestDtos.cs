using DGVisionStudio.Domain.Enums;

namespace DGVisionStudio.Application.DTOs;

public class CreateContactRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpdateContactRequestDto
{
    public ContactRequestStatus Status { get; set; }
    public string? AdminComment { get; set; }
    public bool IsArchived { get; set; }
}
