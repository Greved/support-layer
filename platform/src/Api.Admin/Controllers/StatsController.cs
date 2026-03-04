using Api.Admin.Models.Responses;
using Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
public class StatsController(AppDbContext db) : ControllerBase
{
    [HttpGet("tenants/{id:guid}/stats")]
    public async Task<IActionResult> TenantStats(Guid id)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == id))
            return NotFound();

        var now = DateTime.UtcNow;
        var queries24h = await db.BillingEvents
            .Where(b => b.TenantId == id && b.EventType == "query" && b.CreatedAt >= now.AddHours(-24))
            .CountAsync();
        var queries7d = await db.BillingEvents
            .Where(b => b.TenantId == id && b.EventType == "query" && b.CreatedAt >= now.AddDays(-7))
            .CountAsync();
        var queries30d = await db.BillingEvents
            .Where(b => b.TenantId == id && b.EventType == "query" && b.CreatedAt >= now.AddDays(-30))
            .CountAsync();
        var tokens30d = await db.ChatMessages
            .Where(m => m.Session.TenantId == id && m.CreatedAt >= now.AddDays(-30))
            .SumAsync(m => (long)m.TokensUsed);
        var docCount = await db.Documents.CountAsync(d => d.TenantId == id && d.IsActive);

        return Ok(new TenantStatsResponse(id, queries24h, queries7d, queries30d, tokens30d, docCount,
            LatencyP50Ms: 0, LatencyP95Ms: 0));
    }

    [HttpGet("stats/global")]
    public async Task<IActionResult> GlobalStats()
    {
        var now = DateTime.UtcNow;
        var totalTenants = await db.Tenants.CountAsync();
        var activeTenants = await db.Tenants.CountAsync(t => t.IsActive);
        var queries24h = await db.BillingEvents
            .Where(b => b.EventType == "query" && b.CreatedAt >= now.AddHours(-24))
            .CountAsync();
        var queries7d = await db.BillingEvents
            .Where(b => b.EventType == "query" && b.CreatedAt >= now.AddDays(-7))
            .CountAsync();
        var queries30d = await db.BillingEvents
            .Where(b => b.EventType == "query" && b.CreatedAt >= now.AddDays(-30))
            .CountAsync();
        var tokens30d = await db.ChatMessages
            .Where(m => m.CreatedAt >= now.AddDays(-30))
            .SumAsync(m => (long)m.TokensUsed);
        var totalDocs = await db.Documents.CountAsync(d => d.IsActive);

        return Ok(new GlobalStatsResponse(
            totalTenants, activeTenants, queries24h, queries7d, queries30d, tokens30d, totalDocs));
    }
}
