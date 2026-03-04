using Api.Admin.Models.Responses;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/audit-logs")]
[Authorize]
public class AuditLogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = db.AuditLogs
            .Include(a => a.Tenant)
            .Include(a => a.User)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogResponse(
                a.Id, a.TenantId, a.Tenant.Slug,
                a.UserId, a.User != null ? a.User.Email : null,
                a.Action, a.ResourceType, a.ResourceId,
                a.Metadata, a.IpAddress, a.CreatedAt))
            .ToListAsync();

        return Ok(new PagedResponse<AuditLogResponse>(items, total, page, pageSize));
    }
}
