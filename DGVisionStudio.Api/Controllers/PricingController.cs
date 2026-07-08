using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/pricing")]
public class PricingController(IPricingService pricingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await pricingService.GetActiveAsync());
    }
}

public record PricingItemResponse(
    int Id,
    string Title,
    string Description,
    string PricingMode,
    string? PriceText,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAtUtc
);
