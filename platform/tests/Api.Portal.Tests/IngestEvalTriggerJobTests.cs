using Api.Portal.Constants;
using Api.Portal.Jobs;
using Api.Portal.Services;
using Api.Portal.Tests.Helpers;
using Core.Data;
using Core.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class IngestEvalTriggerJobTests
{
    private PortalApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PortalApiFactory();
        await _factory.InitAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private IServiceScope Scope() => _factory.Services.CreateScope();

    [Test]
    public async Task RunAsync_ReadyDocument_TriggersRagEvalAndStoresRunResults()
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<IngestEvalTriggerJob>();
        var ragClient = (StubRagClient)scope.ServiceProvider.GetRequiredService<IRagClient>();

        var tenant = await SeedHelper.SeedTenantAsync(db, $"ingest-eval-{Guid.NewGuid():N}");
        await SeedHelper.SeedEvalDatasetAsync(
            db,
            tenant,
            question: "What does the guide cover?",
            groundTruth: "It covers support workflow basics.",
            questionType: "synthetic_simple");
        await SeedHelper.SeedEvalDatasetAsync(
            db,
            tenant,
            question: "How should edge cases be handled?",
            groundTruth: "Escalate with low-confidence flagging.",
            questionType: "synthetic_adversarial");

        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            FileName = "guide.md",
            StoragePath = "test-storage-path",
            Status = DocumentStatus.Ready,
            SizeBytes = 1024,
            ChunkCount = 4,
            ContentType = "text/markdown",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        await job.RunAsync(document.Id);

        var run = await db.EvalRuns
            .Where(r => r.TenantId == tenant.Id)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();
        run.Should().NotBeNull();
        run!.RunType.Should().Be("ingest");
        run.Status.Should().Be("completed");

        var resultCount = await db.EvalResults.CountAsync(r => r.RunId == run.Id);
        resultCount.Should().Be(2);

        ragClient.TriggeredEvalRuns.Should().HaveCount(1);
        ragClient.TriggeredEvalRuns[0].TenantSlug.Should().Be(tenant.Slug);
        ragClient.TriggeredEvalRuns[0].TriggerReason.Should().Contain(document.Id.ToString());
    }

    [Test]
    public async Task RunAsync_WhenRunAlreadyInProgress_SkipsTrigger()
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<IngestEvalTriggerJob>();
        var ragClient = (StubRagClient)scope.ServiceProvider.GetRequiredService<IRagClient>();

        var tenant = await SeedHelper.SeedTenantAsync(db, $"ingest-eval-lock-{Guid.NewGuid():N}");
        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            FileName = "faq.md",
            StoragePath = "test-storage-path",
            Status = DocumentStatus.Ready,
            SizeBytes = 512,
            ChunkCount = 2,
            ContentType = "text/markdown",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        await SeedHelper.SeedEvalRunAsync(db, tenant, status: "running", runType: "manual");
        var beforeCount = await db.EvalRuns.CountAsync(r => r.TenantId == tenant.Id);

        await job.RunAsync(document.Id);

        var afterCount = await db.EvalRuns.CountAsync(r => r.TenantId == tenant.Id);
        afterCount.Should().Be(beforeCount);
        ragClient.TriggeredEvalRuns.Should().BeEmpty();
    }
}
