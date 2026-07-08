using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace DGVisionStudio.Api.Services;

public interface IPricingService
{
    Task<IReadOnlyList<PricingItemResponse>> GetActiveAsync();
    Task<IReadOnlyList<PricingItemResponse>> GetAllAsync();
    Task<PricingItemResponse> CreateAsync(PricingItemRequest request);
    Task<PricingItemResponse?> UpdateAsync(int id, PricingItemRequest request);
    Task<IReadOnlyList<PricingItemResponse>> ReorderAsync(ReorderPricingItemsRequest request);
    Task<bool> DeleteAsync(int id);
}

public sealed class PricingService(AppDbContext context) : IPricingService
{
    public async Task<IReadOnlyList<PricingItemResponse>> GetActiveAsync()
    {
        await PricingDataSeeder.EnsureTableAsync(context);

        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Id", "Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc"
            FROM "PricingItems"
            WHERE "IsActive" = TRUE
            ORDER BY "DisplayOrder", "Id"
            """;

        return await ReadPricingItemsAsync(context, command);
    }

    public async Task<IReadOnlyList<PricingItemResponse>> GetAllAsync()
    {
        await PricingDataSeeder.EnsureTableAsync(context);

        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Id", "Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc"
            FROM "PricingItems"
            ORDER BY "DisplayOrder", "Id"
            """;

        return await ReadPricingItemsAsync(context, command);
    }

    public async Task<PricingItemResponse> CreateAsync(PricingItemRequest request)
    {
        await PricingDataSeeder.EnsureTableAsync(context);

        var validationError = Validate(request);
        if (validationError is not null)
            throw new PricingValidationException(validationError);

        var maxOrder = await GetMaxDisplayOrderAsync();
        var pricingMode = NormalizePricingMode(request.PricingMode);
        var priceText = pricingMode == "Negotiable" ? null : Normalize(request.PriceText);

        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO "PricingItems" ("Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc")
            VALUES (@title, @description, @pricingMode, @priceText, @displayOrder, @isActive, NOW())
            RETURNING "Id", "Title", "Description", "PricingMode", "PriceText", "DisplayOrder", "IsActive", "CreatedAtUtc"
            """;

        AddParameter(command, "title", request.Title.Trim());
        AddParameter(command, "description", request.Description.Trim());
        AddParameter(command, "pricingMode", pricingMode);
        AddParameter(command, "priceText", priceText);
        AddParameter(command, "displayOrder", maxOrder + 1);
        AddParameter(command, "isActive", request.IsActive);

        var items = await ReadPricingItemsAsync(context, command);
        return items.First();
    }

    public async Task<PricingItemResponse?> UpdateAsync(int id, PricingItemRequest request)
    {
        await PricingDataSeeder.EnsureTableAsync(context);

        var validationError = Validate(request);
        if (validationError is not null)
            throw new PricingValidationException(validationError);

        var pricingMode = NormalizePricingMode(request.PricingMode);
        var priceText = pricingMode == "Negotiable" ? null : Normalize(request.PriceText);

        using var command = context.Database.GetDbConnection().CreateCommand();
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
        AddParameter(command, "title", request.Title.Trim());
        AddParameter(command, "description", request.Description.Trim());
        AddParameter(command, "pricingMode", pricingMode);
        AddParameter(command, "priceText", priceText);
        AddParameter(command, "isActive", request.IsActive);

        var items = await ReadPricingItemsAsync(context, command);
        return items.FirstOrDefault();
    }

    public async Task<IReadOnlyList<PricingItemResponse>> ReorderAsync(ReorderPricingItemsRequest request)
    {
        await PricingDataSeeder.EnsureTableAsync(context);

        if (request.Ids.Count == 0)
            throw new PricingValidationException("Няма подадени цени за пренареждане.");

        var ids = request.Ids.Distinct().ToList();
        for (var i = 0; i < ids.Count; i++)
        {
            await ExecuteAsync("UPDATE \"PricingItems\" SET \"DisplayOrder\" = @displayOrder WHERE \"Id\" = @id", command =>
            {
                AddParameter(command, "displayOrder", i + 1);
                AddParameter(command, "id", ids[i]);
            });
        }

        return await GetAllAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await PricingDataSeeder.EnsureTableAsync(context);

        var affected = await ExecuteAsync("DELETE FROM \"PricingItems\" WHERE \"Id\" = @id", command =>
        {
            AddParameter(command, "id", id);
        });

        if (affected == 0)
            return false;

        await NormalizeDisplayOrderAsync();
        return true;
    }

    public static string? Validate(PricingItemRequest request)
    {
        var pricingMode = NormalizePricingMode(request.PricingMode);
        if (string.IsNullOrWhiteSpace(request.Title)) return "Заглавието е задължително.";
        if (string.IsNullOrWhiteSpace(request.Description)) return "Описанието е задължително.";
        if (pricingMode == "Fixed" && string.IsNullOrWhiteSpace(request.PriceText)) return "Цената е задължителна при фиксирана цена.";
        return null;
    }

    public static string NormalizePricingMode(string? value) =>
        string.Equals(value, "Negotiable", StringComparison.OrdinalIgnoreCase) ? "Negotiable" : "Fixed";

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<List<PricingItemResponse>> ReadPricingItemsAsync(AppDbContext db, DbCommand command)
    {
        if (command.Connection?.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

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

    private async Task<int> GetMaxDisplayOrderAsync()
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(\"DisplayOrder\"), 0) FROM \"PricingItems\"";

        if (command.Connection?.State != ConnectionState.Open)
            await context.Database.OpenConnectionAsync();

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task NormalizeDisplayOrderAsync()
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Id"
            FROM "PricingItems"
            ORDER BY "DisplayOrder", "Id"
            """;

        if (command.Connection?.State != ConnectionState.Open)
            await context.Database.OpenConnectionAsync();

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
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        configure(command);

        if (command.Connection?.State != ConnectionState.Open)
            await context.Database.OpenConnectionAsync();

        return await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

public sealed class PricingValidationException(string message) : Exception(message);
