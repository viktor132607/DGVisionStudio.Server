using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace DGVisionStudio.Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/pricing")]
public class AdminPricingController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminPricingController(AppDbContext context)
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
            ORDER BY "DisplayOrder", "Id"
            """;

        return Ok(await PricingController.ReadPricingItemsAsync(_context, command));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PricingItemRequest dto)
    {
        await PricingDataSeeder.EnsureTableAsync(_context);

        var validationError = Validate(dto);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var maxOrder = await GetMaxDisplayOrderAsync();
        var pricingMode = NormalizePricingMode(dto.PricingMode);
        var priceText = pricingMode == "Negotiable" ? null : Normalize(dto.PriceText);

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO "PricingItems" ("Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc")
            VALUES (@title, @description, @pricingMode, @priceText, @displayOrder, @isActive, NOW())
            RETURNING "Id", "Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc"
            """;

        AddParameter(command, "title", dto.Title.Trim());
        AddParameter(command, "description", dto.Description.Trim());
        AddParameter(command, "pricingMode", pricingMode);
        AddParameter(command, "priceText", priceText);
        AddParameter(command, "displayOrder", maxOrder + 1);
        AddParameter(command, "isActive", dto.IsActive);

        var items = await PricingController.ReadPricingItemsAsync(_context, command);
        return Ok(items.First());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PricingItemRequest dto)
    {
        await PricingDataSeeder.EnsureTableAsync(_context);

        var validationError = Validate(dto);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var pricingMode = NormalizePricingMode(dto.PricingMode);
        var priceText = pricingMode == "Negotiable" ? null : Normalize(dto.PriceText);

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            UPDATE "PricingItems"
            SET "Title" = @title,
                "Description" = @description,
                "PricingMode" = @pricingMode,
                "PriceText" = @priceText,
                "IsActive" = @isActive
            WHERE "Id" = @id
            RETURNING "Id", "Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc"
            """;

        AddParameter(command, "id", id);
        AddParameter(command, "title", dto.Title.Trim());
        AddParameter(command, "description", dto.Description.Trim());
        AddParameter(command, "pricingMode", pricingMode);
        AddParameter(command, "priceText", priceText);
        AddParameter(command, "isActive", dto.IsActive);

        var items = await PricingController.ReadPricingItemsAsync(_context, command);
        return items.Count == 0 ? NotFound() : Ok(items.First());
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderPricingItemsRequest dto)
    {
        await PricingDataSeeder.EnsureTableAsync(_context);
        if (dto.Ids.Count == 0) return BadRequest(new { message = "Няма подадени цени за пренареждане." });

        var ids = dto.Ids.Distinct().ToList();
        for (var i = 0; i < ids.Count; i++)
        {
            await ExecuteAsync("UPDATE \"PricingItems\" SET \"DisplayOrder\" = @displayOrder WHERE \"Id\" = @id", command =>
            {
                AddParameter(command, "displayOrder", i + 1);
                AddParameter(command, "id", ids[i]);
            });
        }

        return await GetAll();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await PricingDataSeeder.EnsureTableAsync(_context);

        var affected = await ExecuteAsync("DELETE FROM \"PricingItems\" WHERE \"Id\" = @id", command =>
        {
            AddParameter(command, "id", id);
        });

        if (affected == 0) return NotFound();
        await NormalizeDisplayOrderAsync();

        return NoContent();
    }

    private async Task<int> GetMaxDisplayOrderAsync()
    {
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(\"DisplayOrder\"), 0) FROM \"PricingItems\"";

        if (command.Connection?.State != ConnectionState.Open)
        {
            await _context.Database.OpenConnectionAsync();
        }

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task NormalizeDisplayOrderAsync()
    {
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Id"
            FROM "PricingItems"
            ORDER BY "DisplayOrder", "Id"
            """;

        if (command.Connection?.State != ConnectionState.Open)
        {
            await _context.Database.OpenConnectionAsync();
        }

        var ids = new List<int>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        }

        for (var i = 0; i < ids.Count; i++)
        {
            await ExecuteAsync("UPDATE \"PricingItems\" SET \"DisplayOrder\" = @displayOrder WHERE \"Id\" = @id", updateCommand =>
            {
                AddParameter(updateCommand, "displayOrder", i + 1);
                AddParameter(updateCommand, "id", ids[i]);
            });
        }
    }

    private async Task<int> ExecuteAsync(string sql, Action<DbCommand> configure)
    {
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        configure(command);

        if (command.Connection?.State != ConnectionState.Open)
        {
            await _context.Database.OpenConnectionAsync();
        }

        return await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? Validate(PricingItemRequest dto)
    {
        var pricingMode = NormalizePricingMode(dto.PricingMode);
        if (string.IsNullOrWhiteSpace(dto.Title)) return "Заглавието е задължително.";
        if (string.IsNullOrWhiteSpace(dto.Description)) return "Описанието е задължително.";
        if (pricingMode == "Fixed" && string.IsNullOrWhiteSpace(dto.PriceText)) return "Цената е задължителна при фиксирана цена.";
        return null;
    }

    private static string NormalizePricingMode(string? value) =>
        string.Equals(value, "Negotiable", StringComparison.OrdinalIgnoreCase) ? "Negotiable" : "Fixed";

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
