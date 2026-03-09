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
    public const string EvalDatasetVersion = "e2e-seed-v1";
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

        var datasets = new List<EvalDataset>
        {
            new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Question = "E2E quality: what is the refund policy?",
                GroundTruth = "Refunds are accepted within 30 days with receipt.",
                SourceChunkIdsJson = "[\"chunk-refund-policy\"]",
                QuestionType = "positive",
                DatasetVersion = EvalDatasetVersion,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            },
        };

        var realEvalSetting = Environment.GetEnvironmentVariable("E2E_REQUIRE_REAL_EVAL");
        var realEvalEnabled = !string.Equals(realEvalSetting?.Trim(), "0", StringComparison.Ordinal);
        if (!realEvalEnabled)
        {
            datasets.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Question = "E2E quality negative: can I get a 200% refund?",
                GroundTruth = "No, maximum refund is 100% of purchase amount.",
                SourceChunkIdsJson = "[\"chunk-refund-limits\"]",
                QuestionType = "negative",
                DatasetVersion = EvalDatasetVersion,
                CreatedAt = DateTime.UtcNow.AddMinutes(-4),
            });
            datasets.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Question = "E2E quality: where can I manage billing?",
                GroundTruth = "Billing can be managed in account settings under Billing.",
                SourceChunkIdsJson = "[\"chunk-billing-settings\"]",
                QuestionType = "positive",
                DatasetVersion = EvalDatasetVersion,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            });
        }

        db.EvalDatasets.AddRange(datasets);

        await db.SaveChangesAsync();
    }
}
