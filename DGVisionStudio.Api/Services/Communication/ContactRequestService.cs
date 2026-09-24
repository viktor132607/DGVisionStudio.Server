using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class ContactRequestService : IContactRequestService
{
    private readonly ContactRequestSubmissionService submissions;
    private readonly ContactRequestQueryService queries;
    private readonly ContactRequestAdminCommandService commands;

    [ActivatorUtilitiesConstructor]
    public ContactRequestService(
        ContactRequestSubmissionService submissions,
        ContactRequestQueryService queries,
        ContactRequestAdminCommandService commands)
    {
        this.submissions = submissions;
        this.queries = queries;
        this.commands = commands;
    }

    public ContactRequestService(
        AppDbContext context,
        IEmailService emailService,
        IConfiguration configuration)
        : this(
            new ContactRequestSubmissionService(
                context,
                new ContactRequestInputValidator(),
                new ContactRequestFactory(),
                new ContactRequestNotificationService(
                    context,
                    emailService,
                    configuration)),
            new ContactRequestQueryService(context),
            new ContactRequestAdminCommandService(context))
    {
    }

    public ContactRequestService(AppDbContext context)
        : this(
            new ContactRequestSubmissionService(
                context,
                new ContactRequestInputValidator(),
                new ContactRequestFactory(),
                new ContactRequestNotificationService(context)),
            new ContactRequestQueryService(context),
            new ContactRequestAdminCommandService(context))
    {
    }

    public Task<ControllerServiceResult> CreateAsync(
        CreateContactRequestDto dto) =>
        submissions.CreateAsync(dto);

    public Task<ControllerServiceResult> MarkAllSeenAsync() =>
        commands.MarkAllSeenAsync();

    public Task<ControllerServiceResult> GetAllAsync() =>
        queries.GetAllAsync();

    public Task<ControllerServiceResult> GetAsync(Guid id) =>
        queries.GetAsync(id);

    public Task<ControllerServiceResult> UpdateAsync(
        Guid id,
        UpdateContactRequestDto dto) =>
        commands.UpdateAsync(id, dto);

    public Task<ControllerServiceResult> UpdateStatusAsync(
        Guid id,
        UpdateContactRequestDto dto) =>
        commands.UpdateStatusAsync(id, dto);

    public Task<ControllerServiceResult> DeleteAsync(Guid id) =>
        commands.DeleteAsync(id);
}
