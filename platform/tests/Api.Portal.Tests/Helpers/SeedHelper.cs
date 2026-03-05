using Core.Data;
using Core.Entities;

namespace Api.Portal.Tests.Helpers;

public static class SeedHelper
{
    public static async Task<Tenant> SeedTenantAsync(AppDbContext db, string slug = "test-corp")
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

    public static async Task<User> SeedUserAsync(AppDbContext db, Tenant tenant, string email = "owner@test.com", string roleSlug = "owner")
    {
        var existing = db.Users.FirstOrDefault(u => u.Email == email && u.TenantId == tenant.Id);
        if (existing is not null) return existing;

        var role = db.Roles.First(r => r.Slug == roleSlug);
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<BillingEvent> SeedBillingEventAsync(AppDbContext db, Tenant tenant, string eventType = "query")
    {
        var evt = new BillingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            EventType = eventType,
            Amount = 0,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
        };
        db.BillingEvents.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }
}
