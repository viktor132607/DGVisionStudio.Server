using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Route("api/pricing")]
public class PricingController : ControllerBase
{
    private readonly AppDbContext _context;

    public PricingController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await PricingDataSeeder.EnsureTableAsync(_context);

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Id", "Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc"
            FROM "PricingItems"
            WHERE "IsActive" = TRUE
            ORDER BY "DisplayOrder", "Id"
            """;

        var items = await ReadPricingItemsAsync(_context, command);
        return Ok(items);
    }

    internal static async Task<List<PricingItemResponse>> ReadPricingItemsAsync(AppDbContext db, DbCommand command)
    {
        if (command.Connection?.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync();
        }

        var items = new List<PricingItemResponse>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new PricingItemResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5),
                reader.GetBoolean(6),
                reader.GetDateTime(7)
            ));
        }

        return items;
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
