using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminPrintRequestQueryService
{
    private readonly AdminDirectPrintRequestQueryService directRequests;
    private readonly AdminUploadedPrintRequestQueryService uploadedRequests;

    [ActivatorUtilitiesConstructor]
    public AdminPrintRequestQueryService(
        AdminDirectPrintRequestQueryService directRequests,
        AdminUploadedPrintRequestQueryService uploadedRequests)
    {
        this.directRequests = directRequests;
        this.uploadedRequests = uploadedRequests;
    }

    public AdminPrintRequestQueryService(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mapper = new AdminPrintRequestMapper();
        directRequests = new AdminDirectPrintRequestQueryService(
            context,
            mapper);
        uploadedRequests = new AdminUploadedPrintRequestQueryService(
            context,
            mapper);
    }

    public async Task<ControllerServiceResult> GetAllAsync()
    {
        var direct = await directRequests.GetAllAsync();
        var uploaded = await uploadedRequests.GetAllAsync();

        var result = direct
            .Concat(uploaded)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        return ControllerServiceResult.Ok(result);
    }

    public async Task<ControllerServiceResult> GetByIdAsync(int id)
    {
        var dto = id < 0
            ? await uploadedRequests.GetByAlbumIdAsync(Math.Abs(id))
            : await directRequests.GetByIdAsync(id);

        return dto == null
            ? ControllerServiceResult.NotFound()
            : ControllerServiceResult.Ok(dto);
    }
}
