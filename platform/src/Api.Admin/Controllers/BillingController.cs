using Api.Admin.Models.Responses;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/tenants/{tenantId:guid}/billing")]
[Authorize]
public class BillingController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid tenantId)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound();

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var events = await db.BillingEvents
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(50)
            .ToListAsync();

        var total30d = await db.BillingEvents
            .Where(b => b.TenantId == tenantId && b.CreatedAt >= cutoff)
            .SumAsync(b => b.Amount);

        var count30d = await db.BillingEvents
            .Where(b => b.TenantId == tenantId && b.CreatedAt >= cutoff)
            .CountAsync();

        var eventResponses = events
            .Select(e => new BillingEventResponse(
                e.Id, e.EventType, e.Amount, e.Currency, e.ExternalRef, e.CreatedAt))
            .ToList();

        return Ok(new TenantBillingResponse(tenantId, total30d, count30d, eventResponses));
    }
}
