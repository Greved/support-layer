using Core.Data;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Portal.E2e.Tests;

/// <summary>
/// Seeds a deterministic E2E test tenant and owner user with known credentials.
/// Idempotent — safe to call multiple times.
/// </summary>
public static class E2eSeeder
{
    // Fixed IDs and credentials for E2E tests
    public static readonly Guid TenantId = new("e2e00000-0000-0000-0000-000000000001");
    public static readonly Guid UserId = new("e2e00000-0000-0000-0000-000000000002");
    public const string UserEmail = "e2e@test.com";
    public const string UserPassword = "E2ePassword123!";
    public const string TenantSlug = "e2e-corp";

    public static async Task SeedDefaultUserAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();

        if (await db.Tenants.AnyAsync(t => t.Id == TenantId))
            return;

        var freePlan = await db.Plans.FirstAsync(p => p.Slug == "free");
        var ownerRole = await db.Roles.FirstAsync(r => r.Slug == "owner");

        var tenant = new Tenant
        {
            Id = TenantId,
            Name = "E2E Corp",
            Slug = TenantSlug,
            PlanId = freePlan.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);

        var user = new User
        {
            Id = UserId,
            TenantId = TenantId,
            Email = UserEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(UserPassword),
            RoleId = ownerRole.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);

        await db.SaveChangesAsync();
    }
}
