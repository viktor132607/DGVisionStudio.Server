using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/admin/commands")]
public sealed class AdminCommandsController(AdminCommandService service) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] AdminCommandRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.PreviewAsync(request, cancellationToken));

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] AdminCommandRequest request, CancellationToken cancellationToken) =>
        this.ToActionResult(await service.ExecuteAsync(
            request, this.CreateAdminRequestContext(), cancellationToken));
}
