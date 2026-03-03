using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/config")]
[Authorize]
public class ConfigController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetConfig()
    {
        var configs = await db.TenantConfigs
            .Where(c => c.TenantId == tenantContext.TenantId)
            .ToListAsync();

        return Ok(configs.ToDictionary(c => c.Key, c => c.Value));
    }

    [HttpPut]
    public async Task<IActionResult> UpsertConfig([FromBody] Dictionary<string, string> config)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var existing = await db.TenantConfigs
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.Key);

        foreach (var (key, value) in config)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.Value = value;
                row.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.TenantConfigs.Add(new TenantConfig
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }
}
