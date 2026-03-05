using Api.Portal.Models.Responses;
using Core.Auth;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/dashboard")]
[Authorize]
public class DashboardController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<DashboardUsageResponse>> GetUsage()
    {
        var tenantId = tenantContext.TenantId;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var queriesThisMonth = await db.BillingEvents
            .CountAsync(b => b.TenantId == tenantId
                           && b.EventType == "query"
                           && b.CreatedAt >= monthStart);

        var documentCount = await db.Documents
            .CountAsync(d => d.TenantId == tenantId && d.IsActive);

        var teamMemberCount = await db.Users
            .CountAsync(u => u.TenantId == tenantId && u.IsActive);

        var tenant = await db.Tenants
            .Include(t => t.Plan).ThenInclude(p => p.Limit)
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        var limit = tenant?.Plan?.Limit;

        return Ok(new DashboardUsageResponse(
            queriesThisMonth,
            documentCount,
            teamMemberCount,
            new PlanLimitsInfo(
                limit?.MaxDocuments ?? 0,
                limit?.MaxQueriesPerMonth ?? 0,
                limit?.MaxUsers ?? 0,
                limit?.MaxFileSizeMb ?? 0
            )
        ));
    }
}
