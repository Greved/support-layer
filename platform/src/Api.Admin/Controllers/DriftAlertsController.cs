using Api.Admin.Models.Responses;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/drift-alerts")]
[Authorize]
public class DriftAlertsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DriftAlertResponse>>> List(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? signal = null,
        [FromQuery] int days = 30)
    {
        var query = db.DriftAlerts.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(signal))
            query = query.Where(a => a.Signal == signal.Trim());
        if (days > 0)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            query = query.Where(a => a.CreatedAt >= from);
        }

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(500)
            .Select(a => new DriftAlertResponse(
                a.Id,
                a.TenantId,
                a.Signal,
                a.BaselineRate,
                a.CurrentRate,
                a.DropAmount,
                a.Threshold,
                a.Reason,
                a.WindowStartUtc,
                a.WindowEndUtc,
                a.CreatedAt
            ))
            .ToListAsync();

        return Ok(rows);
    }
}
