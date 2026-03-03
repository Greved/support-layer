using System.Security.Cryptography;
using System.Text;
using Api.Portal.Models.Requests;
using Api.Portal.Models.Responses;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/api-keys")]
[Authorize]
public class ApiKeysController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ApiKeyResponse>>> GetKeys()
    {
        var keys = await db.ApiKeys
            .Where(k => k.TenantId == tenantContext.TenantId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyResponse(k.Id, k.Name, k.CreatedAt, k.ExpiresAt, k.IsActive))
            .ToListAsync();

        return Ok(keys);
    }

    [HttpPost]
    public async Task<ActionResult<CreateApiKeyResponse>> CreateKey([FromBody] CreateApiKeyRequest request)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = $"sl_live_{Base64UrlEncode(rawBytes)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId!.Value,
            Name = request.Name,
            KeyHash = hash,
            IsActive = true,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
        };

        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();

        return Ok(new CreateApiKeyResponse(key.Id, key.Name, plaintext, key.CreatedAt, key.ExpiresAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteKey(Guid id)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.Id == id && k.TenantId == tenantContext.TenantId);

        if (key is null) return NotFound();

        key.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
