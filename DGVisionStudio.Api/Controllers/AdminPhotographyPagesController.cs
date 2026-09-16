using DGVisionStudio.Api.Extensions;
using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/photography-pages")]
public sealed class AdminPhotographyPagesController(PhotographyPageService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List() => Ok(await service.ListAsync(true));
    [HttpPost] public async Task<IActionResult> Create(PhotographyPageInput input) =>
        this.ToActionResult(await service.SaveAsync(null, input));
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, PhotographyPageInput input) =>
        this.ToActionResult(await service.SaveAsync(id, input));
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id) =>
        this.ToActionResult(await service.DeleteAsync(id));
}
