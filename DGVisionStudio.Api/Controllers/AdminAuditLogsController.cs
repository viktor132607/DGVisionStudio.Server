using DGVisionStudio.Application.DTOs.AuditLogs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/audit-logs")]
public class AdminAuditLogsController : ControllerBase
{
	private readonly AppDbContext _context;

	public AdminAuditLogsController(AppDbContext context)
	{
		_context = context;
	}

	[HttpGet]
	public async Task<IActionResult> GetAuditLogs(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 50,
		[FromQuery] string? entityType = null,
		[FromQuery] string? entityId = null,
		[FromQuery] string? adminEmail = null,
		[FromQuery] string? action = null)
	{
		page = page < 1 ? 1 : page;
		pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

		var query = _context.Set<AuditLog>()
			.AsNoTracking()
			.AsQueryable();

		if (!string.IsNullOrWhiteSpace(entityType))
		{
			query = query.Where(x => x.EntityType == entityType.Trim());
		}

		if (!string.IsNullOrWhiteSpace(entityId))
		{
			query = query.Where(x => x.EntityId == entityId.Trim());
		}

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
			.Select(x => new AuditLogDto
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
			})
			.ToListAsync();

		return Ok(new
		{
			page,
			pageSize,
			total,
			items
		});
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetAuditLogById([FromRoute] int id)
	{
		var item = await _context.Set<AuditLog>()
			.AsNoTracking()
			.Where(x => x.Id == id)
			.Select(x => new AuditLogDto
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
			})
			.FirstOrDefaultAsync();

		if (item == null)
			return NotFound(new { message = "Audit log not found." });

		return Ok(item);
	}
}