using Core.Data;
using Core.Entities;

namespace Api.Admin.Tests.Helpers;

public static class SeedHelper
{
    public static readonly Guid PlanFreeId = new("00000001-0000-0000-0000-000000000001");

    public static async Task<AdminUser> SeedAdminUserAsync(AppDbContext db, string email = "admin@test.com")
    {
        var existing = db.AdminUsers.FirstOrDefault(a => a.Email == email);
        if (existing is not null) return existing;

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Name = "Test Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    public static async Task<Tenant> SeedTenantAsync(AppDbContext db, string slug = "acme")
    {
        var existing = db.Tenants.FirstOrDefault(t => t.Slug == slug);
        if (existing is not null) return existing;

        var plan = db.Plans.First(); // seeded by migration

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = slug.ToUpperInvariant(),
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

    public static async Task<User> SeedUserAsync(AppDbContext db, Tenant tenant, string email = "owner@acme.com")
    {
        var existing = db.Users.FirstOrDefault(u => u.Email == email && u.TenantId == tenant.Id);
        if (existing is not null) return existing;

        var role = db.Roles.First(r => r.Slug == "owner");

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

    public static async Task<Document> SeedDocumentAsync(AppDbContext db, Tenant tenant)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            FileName = "test.pdf",
            StoragePath = "/uploads/test.pdf",
            Status = "ready",
            SizeBytes = 1024,
            ChunkCount = 5,
            ContentType = "application/pdf",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    public static async Task<BillingEvent> SeedBillingEventAsync(AppDbContext db, Tenant tenant)
    {
        var evt = new BillingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            EventType = "query",
            Amount = 0,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
        };
        db.BillingEvents.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }
}
