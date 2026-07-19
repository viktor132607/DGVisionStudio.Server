using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.AuditLogs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed class AdminAuditLogQueryService : IAdminAuditLogQueryService
{
    private readonly AppDbContext _context;

    public AdminAuditLogQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ControllerServiceResult> GetAuditLogsAsync(
        int page,
        int pageSize,
        string? entityType,
        string? entityId,
        string? adminEmail,
        string? action)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var query = _context.Set<AuditLog>()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType.Trim());

        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(x => x.EntityId == entityId.Trim());

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var normalizedEmail = adminEmail.Trim().ToLowerInvariant();
            query = query.Where(x => x.AdminEmail.ToLower().Contains(normalizedEmail));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToLowerInvariant();
            query = query.Where(x => x.Action.ToLower().Contains(normalizedAction));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Map())
            .ToListAsync();

        return ControllerServiceResult.Ok(new { page, pageSize, total, items });
    }

    public async Task<ControllerServiceResult> GetAuditLogByIdAsync(int id)
    {
        var item = await _context.Set<AuditLog>()
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(Map())
            .FirstOrDefaultAsync();

        return item is null
            ? ControllerServiceResult.NotFound(new { message = "Audit log not found." })
            : ControllerServiceResult.Ok(item);
    }

    private static System.Linq.Expressions.Expression<Func<AuditLog, AuditLogDto>> Map() =>
        x => new AuditLogDto
        {
            Id = x.Id,
            AdminUserId = x.AdminUserId,
            AdminEmail = x.AdminEmail,
            Action = x.Action,
            EntityType = x.EntityType,
            EntityId = x.EntityId,
            OldValue = x.OldValue,
            NewValue = x.NewValue,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            TraceId = x.TraceId,
            CreatedAtUtc = x.CreatedAtUtc
        };
}
