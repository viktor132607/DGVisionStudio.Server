using DGVisionStudio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/pricing")]
public class AdminPricingController(IPricingService pricingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await pricingService.GetAllAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PricingItemRequest dto)
    {
        try
        {
            return Ok(await pricingService.CreateAsync(dto));
        }
        catch (PricingValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PricingItemRequest dto)
    {
        try
        {
            var updatedItem = await pricingService.UpdateAsync(id, dto);
            return updatedItem is null ? NotFound() : Ok(updatedItem);
        }
        catch (PricingValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderPricingItemsRequest dto)
    {
        try
        {
            return Ok(await pricingService.ReorderAsync(dto));
        }
        catch (PricingValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await pricingService.DeleteAsync(id) ? NoContent() : NotFound();
    }
}

public class PricingItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PricingMode { get; set; } = "Fixed";
    public string? PriceText { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ReorderPricingItemsRequest
{
    public List<int> Ids { get; set; } = [];
}
