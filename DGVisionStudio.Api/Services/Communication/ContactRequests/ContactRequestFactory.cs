using DGVisionStudio.Domain.Entities;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestFactory
{
    public ContactRequest Create(ContactRequestSubmissionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            Email = input.Email,
            Phone = input.Phone,
            Subject = input.Subject,
            Message = input.Message,
            IsSeenByAdmin = false,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
