using DGVisionStudio.Api.Services.Interfaces;

namespace DGVisionStudio.Api.Services;

public sealed class HomeStatusService : IHomeStatusService
{
    public ControllerServiceResult GetStatus() =>
        ControllerServiceResult.Ok(new { message = "DG Vision Studio API running" });
}
