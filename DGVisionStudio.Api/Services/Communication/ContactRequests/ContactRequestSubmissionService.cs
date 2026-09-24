using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestSubmissionService(
    AppDbContext context,
    ContactRequestInputValidator validator,
    ContactRequestFactory factory,
    ContactRequestNotificationService notifications)
{
    public async Task<ControllerServiceResult> CreateAsync(
        CreateContactRequestDto dto)
    {
        var validation = validator.Validate(dto);
        if (!validation.IsValid)
            return validation.Error!;

        var entity = factory.Create(validation.Input!);

        context.ContactRequests.Add(entity);
        await context.SaveChangesAsync();

        await notifications.SendOwnerNotificationAsync(entity);

        return ControllerServiceResult.Ok(new
        {
            message = "Contact request submitted successfully.",
            id = entity.Id
        });
    }
}
