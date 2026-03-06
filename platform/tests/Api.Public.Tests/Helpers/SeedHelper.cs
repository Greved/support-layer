using System.Security.Cryptography;
using System.Text;
using Core.Data;
using Core.Entities;

namespace Api.Public.Tests.Helpers;

public static class SeedHelper
{
    public static async Task<(Tenant tenant, ApiKey key, string plaintext)> SeedTenantWithApiKeyAsync(
        AppDbContext db, string slug = "public-test-corp")
    {
        var tenant = await EnsureTenantAsync(db, slug);
        var (apiKey, plaintext) = await CreateApiKeyForTenantAsync(db, tenant.Id, "Test Key");
        return (tenant, apiKey, plaintext);
    }

    public static async Task<Tenant> EnsureTenantAsync(AppDbContext db, string slug)
    {
        var existing = db.Tenants.FirstOrDefault(t => t.Slug == slug);
        if (existing is not null) return existing;

        var plan = db.Plans.First();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = slug,
            Slug = slug,
            PlanId = plan.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    public static async Task<(ApiKey key, string plaintext)> CreateApiKeyForTenantAsync(
        AppDbContext db,
        Guid tenantId,
        string name,
        bool isActive = true,
        DateTime? expiresAt = null)
    {
        var now = DateTime.UtcNow;

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = $"sl_live_{Base64UrlEncode(rawBytes)}";
        // Must match ApiKeyMiddleware.ComputeSha256Hex which uses ToHexStringLower
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            KeyHash = hash,
            IsActive = isActive,
            ExpiresAt = expiresAt,
            CreatedAt = now,
        };

        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();

        return (apiKey, plaintext);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
