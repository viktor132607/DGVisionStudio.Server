using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/portfolio/albums")]
public sealed class AdminPortfolioAlbumBulkController(PortfolioAlbumBulkService service) : ControllerBase
{
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> Delete(AlbumSelectionRequest request, CancellationToken token) =>
        this.ToActionResult(await service.ExecuteAsync(request.AlbumIds, null, this.CreateAdminRequestContext(), token));

    [HttpPost("bulk-move")]
    public async Task<IActionResult> Move(AlbumMoveRequest request, CancellationToken token) =>
        this.ToActionResult(await service.ExecuteAsync(request.AlbumIds, request.CategoryId, this.CreateAdminRequestContext(), token));
}
