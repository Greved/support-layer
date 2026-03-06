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

    public static async Task<ChatSession> SeedChatSessionAsync(AppDbContext db, Tenant tenant, User? user = null)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user?.Id,
            Title = "Test session",
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
        string role = "assistant",
        string content = "Sample answer content")
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    public static async Task<ChatMessageFeedback> SeedFeedbackAsync(
        AppDbContext db,
        Tenant tenant,
        ChatMessage message,
        string rating = "down",
        string? comment = "Needs correction",
        bool flagged = true,
        bool promoted = false)
    {
        var feedback = new ChatMessageFeedback
        {
            Id = Guid.NewGuid(),
            ChatMessageId = message.Id,
            TenantId = tenant.Id,
            Rating = rating,
            Comment = comment,
            Flagged = flagged,
            Promoted = promoted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChatMessageFeedback.Add(feedback);
        await db.SaveChangesAsync();
        return feedback;
    }

    public static async Task<DriftAlert> SeedDriftAlertAsync(
        AppDbContext db,
        Tenant tenant,
        string signal = "thumbs_up_rate_drop",
        double baselineRate = 0.70,
        double currentRate = 0.50,
        double threshold = 0.10)
    {
        var alert = new DriftAlert
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Signal = signal,
            BaselineRate = baselineRate,
            CurrentRate = currentRate,
            DropAmount = baselineRate - currentRate,
            Threshold = threshold,
            Reason = $"Thumbs-up rate dropped from {baselineRate:P1} to {currentRate:P1}.",
            WindowStartUtc = DateTime.UtcNow.AddDays(-7),
            WindowEndUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        db.DriftAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert;
    }

    public static async Task<EvalDataset> SeedEvalDatasetAsync(
        AppDbContext db,
        Tenant tenant,
        string question = "How to reset password?",
        string groundTruth = "Use the reset form.",
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
        string runType = "manual",
        string status = "completed")
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
        double faithfulness = 0.9,
        double relevancy = 0.88,
        double precision = 0.87,
        double recall = 0.86,
        double hallucination = 0.06)
    {
        var result = new EvalResult
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            DatasetItemId = dataset?.Id,
            Answer = dataset?.GroundTruth ?? "answer",
            RetrievedChunksJson = "[]",
            Faithfulness = faithfulness,
            AnswerRelevancy = relevancy,
            ContextPrecision = precision,
            ContextRecall = recall,
            HallucinationScore = hallucination,
            AnswerCompleteness = 0.89,
            LatencyMs = 150,
            CreatedAt = DateTime.UtcNow,
        };
        db.EvalResults.Add(result);
        await db.SaveChangesAsync();
        return result;
    }

    public static async Task<EvalBaseline> SeedEvalBaselineAsync(
        AppDbContext db,
        Tenant tenant,
        EvalRun run,
        string setBy = "tests")
    {
        var row = new EvalBaseline
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            RunId = run.Id,
            SetAt = DateTime.UtcNow,
            SetBy = setBy,
        };
        db.EvalBaselines.Add(row);
        await db.SaveChangesAsync();
        return row;
    }
}
