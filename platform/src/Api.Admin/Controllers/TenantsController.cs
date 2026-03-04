using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Admin.Models.Requests;
using Api.Admin.Models.Responses;
using Api.Admin.Services;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/tenants")]
[Authorize]
public class TenantsController(AppDbContext db, IAdminTokenService tokenService, IQdrantAdminService qdrant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? plan,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = db.Tenants
            .Include(t => t.Plan)
            .Include(t => t.Users)
            .Include(t => t.Documents)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || t.Slug.Contains(search));
        if (!string.IsNullOrWhiteSpace(plan))
            query = query.Where(t => t.Plan.Slug == plan);
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TenantResponse(
                t.Id, t.Name, t.Slug, t.Plan.Slug, t.IsActive,
                t.Users.Count, t.Documents.Count, t.CreatedAt, t.UpdatedAt))
            .ToListAsync();

        return Ok(new PagedResponse<TenantResponse>(items, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var tenant = await db.Tenants
            .Include(t => t.Plan)
            .Include(t => t.Users)
            .Include(t => t.Documents)
            .Include(t => t.ApiKeys)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null) return NotFound();

        var queries30d = await db.BillingEvents
            .Where(b => b.TenantId == id && b.EventType == "query"
                        && b.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .CountAsync();

        var tokens30d = await db.ChatMessages
            .Where(m => m.Session.TenantId == id
                        && m.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .SumAsync(m => (long)m.TokensUsed);

        return Ok(new TenantDetailResponse(
            tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.Slug, tenant.IsActive,
            tenant.CreatedAt, tenant.UpdatedAt,
            tenant.Users.Count, tenant.Documents.Count, tenant.ApiKeys.Count,
            queries30d, tokens30d));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Slug == request.PlanSlug);
        if (plan is null) return BadRequest(new { error = "Unknown plan." });

        if (await db.Tenants.AnyAsync(t => t.Slug == request.Slug))
            return Conflict(new { error = "Slug already in use." });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            PlanId = plan.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = tenant.Id },
            new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, plan.Slug,
                true, 0, 0, tenant.CreatedAt, tenant.UpdatedAt));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        if (request.PlanSlug is not null)
        {
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Slug == request.PlanSlug);
            if (plan is null) return BadRequest(new { error = "Unknown plan." });
            tenant.PlanId = plan.Id;
            tenant.Plan = plan;
        }

        if (request.IsActive.HasValue)
            tenant.IsActive = request.IsActive.Value;

        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new TenantResponse(
            tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.Slug, tenant.IsActive,
            0, 0, tenant.CreatedAt, tenant.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();

        // Soft-delete
        tenant.IsActive = false;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Purge Qdrant collection (fire-and-forget; collection may not exist)
        _ = Task.Run(async () =>
        {
            try { await qdrant.DeleteCollectionAsync($"tenant_{tenant.Slug}"); }
            catch { /* ignore */ }
        });

        return NoContent();
    }

    [HttpPost("{id:guid}/impersonate")]
    public async Task<IActionResult> Impersonate(Guid id)
    {
        var tenant = await db.Tenants
            .Include(t => t.Users).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null) return NotFound();

        var adminUser = tenant.Users.FirstOrDefault(u => u.Role.Slug is "owner" or "admin")
                     ?? tenant.Users.FirstOrDefault();

        if (adminUser is null)
            return BadRequest(new { error = "Tenant has no users to impersonate." });

        var adminId = User.FindFirst("sub")?.Value ?? string.Empty;
        Guid.TryParse(adminId, out var adminGuid);

        var token = tokenService.IssueImpersonationToken(
            tenant, adminUser.Id, adminUser.Email, adminUser.Role.Slug, adminGuid);

        // Write audit log
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = id,
            UserId = null,
            Action = "impersonate",
            ResourceType = "tenant",
            ResourceId = id.ToString(),
            Metadata = $"{{\"impersonated_by\":\"{adminGuid}\",\"as_user\":\"{adminUser.Id}\"}}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return Ok(new ImpersonateResponse(token, 900));
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id)
    {
        var tenant = await db.Tenants
            .Include(t => t.Users)
            .Include(t => t.Documents)
            .Include(t => t.Configs)
            .Include(t => t.ApiKeys)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null) return NotFound();

        var sessions = await db.ChatSessions
            .Include(s => s.Messages)
            .Where(s => s.TenantId == id)
            .ToListAsync();

        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void AddJson(string name, object data)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }

            AddJson("tenant.json", new { tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt });
            AddJson("documents.json", tenant.Documents.Select(d => new { d.Id, d.FileName, d.Status, d.ChunkCount, d.CreatedAt }));
            AddJson("configs.json", tenant.Configs.Select(c => new { c.Key, c.Value }));
            AddJson("api_keys.json", tenant.ApiKeys.Select(k => new { k.Id, k.Name, k.IsActive, k.CreatedAt }));
            AddJson("chat_sessions.json", sessions.Select(s => new
            {
                s.Id,
                s.CreatedAt,
                Messages = s.Messages.Select(m => new { m.Role, m.Content, m.CreatedAt }),
            }));
        }

        stream.Position = 0;
        return File(stream, "application/zip", $"tenant_{tenant.Slug}_export.zip");
    }
}
