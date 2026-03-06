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

    public static async Task<ChatSession> SeedChatSessionAsync(AppDbContext db, Tenant tenant, User? user = null)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user?.Id,
            Title = "Drift test session",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public static async Task<ChatMessage> SeedChatMessageAsync(
        AppDbContext db,
        ChatSession session,
        string role,
        string content,
        DateTime createdAt)
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = role,
            Content = content,
            CreatedAt = createdAt,
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    public static async Task<ChatMessageFeedback> SeedFeedbackAsync(
        AppDbContext db,
        Tenant tenant,
        ChatMessage message,
        string rating,
        DateTime createdAt)
    {
        var feedback = new ChatMessageFeedback
        {
            Id = Guid.NewGuid(),
            ChatMessageId = message.Id,
            TenantId = tenant.Id,
            Rating = rating,
            Comment = null,
            Flagged = false,
            Promoted = false,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        db.ChatMessageFeedback.Add(feedback);
        await db.SaveChangesAsync();
        return feedback;
    }

    public static async Task<EvalDataset> SeedEvalDatasetAsync(
        AppDbContext db,
        Tenant tenant,
        string question = "Question?",
        string groundTruth = "Ground truth",
        string questionType = "synthetic")
    {
        var row = new EvalDataset
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            SourceFeedbackId = null,
            Question = question,
            GroundTruth = groundTruth,
            SourceChunkIdsJson = "[]",
            QuestionType = questionType,
            DatasetVersion = "test",
            CreatedAt = DateTime.UtcNow,
        };
        db.EvalDatasets.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public static async Task<EvalRun> SeedEvalRunAsync(
        AppDbContext db,
        Tenant tenant,
        string status = "completed",
        string runType = "manual")
    {
        var now = DateTime.UtcNow;
        var run = new EvalRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            RunType = runType,
            ConfigSnapshotJson = "{}",
            TriggeredBy = "tests",
            StartedAt = now.AddMinutes(-1),
            FinishedAt = string.Equals(status, "running", StringComparison.OrdinalIgnoreCase) ? null : now,
            Status = status,
            CreatedAt = now,
        };
        db.EvalRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    public static async Task<EvalResult> SeedEvalResultAsync(
        AppDbContext db,
        EvalRun run,
        EvalDataset? dataset = null,
        double faithfulness = 0.9)
    {
        var result = new EvalResult
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            DatasetItemId = dataset?.Id,
            Answer = dataset?.GroundTruth ?? "Answer",
            RetrievedChunksJson = "[]",
            Faithfulness = faithfulness,
            AnswerRelevancy = 0.88,
            ContextPrecision = 0.87,
            ContextRecall = 0.86,
            HallucinationScore = 0.07,
            AnswerCompleteness = 0.89,
            LatencyMs = 145,
            CreatedAt = DateTime.UtcNow,
        };
        db.EvalResults.Add(result);
        await db.SaveChangesAsync();
        return result;
    }
}
