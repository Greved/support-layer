using Api.Portal.Jobs;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class FeedbackDriftDetectionJobTests
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
    public async Task RunAsync_ThumbsUpDropExceedsThreshold_CreatesAlert()
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<FeedbackDriftDetectionJob>();

        var tenant = await SeedHelper.SeedTenantAsync(db, $"drift-job-{Guid.NewGuid():N}");
        var session = await SeedHelper.SeedChatSessionAsync(db, tenant);

        var oldStamp = DateTime.UtcNow.AddDays(-20);
        for (var i = 0; i < 20; i++)
        {
            var message = await SeedHelper.SeedChatMessageAsync(
                db,
                session,
                role: "assistant",
                content: $"old-{i}",
                createdAt: oldStamp.AddMinutes(i));
            await SeedHelper.SeedFeedbackAsync(
                db,
                tenant,
                message,
                rating: i < 14 ? "up" : "down",
                createdAt: oldStamp.AddMinutes(i));
        }

        var currentStamp = DateTime.UtcNow.AddDays(-2);
        for (var i = 0; i < 10; i++)
        {
            var message = await SeedHelper.SeedChatMessageAsync(
                db,
                session,
                role: "assistant",
                content: $"current-{i}",
                createdAt: currentStamp.AddMinutes(i));
            await SeedHelper.SeedFeedbackAsync(
                db,
                tenant,
                message,
                rating: i < 5 ? "up" : "down",
                createdAt: currentStamp.AddMinutes(i));
        }

        await job.RunAsync();
        await job.RunAsync(); // repeat to verify dedupe window uniqueness

        var alerts = db.DriftAlerts.Where(a => a.TenantId == tenant.Id).ToList();
        alerts.Should().HaveCount(1);
        alerts[0].Signal.Should().Be("thumbs_up_rate_drop");
        alerts[0].DropAmount.Should().BeGreaterThan(0.10);
        alerts[0].Reason.Should().Contain("Thumbs-up rate dropped");
    }

    [Test]
    public async Task RunAsync_DropBelowThreshold_DoesNotCreateAlert()
    {
        using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<FeedbackDriftDetectionJob>();

        var tenant = await SeedHelper.SeedTenantAsync(db, $"drift-job-safe-{Guid.NewGuid():N}");
        var session = await SeedHelper.SeedChatSessionAsync(db, tenant);

        var oldStamp = DateTime.UtcNow.AddDays(-20);
        for (var i = 0; i < 20; i++)
        {
            var message = await SeedHelper.SeedChatMessageAsync(
                db,
                session,
                role: "assistant",
                content: $"safe-old-{i}",
                createdAt: oldStamp.AddMinutes(i));
            await SeedHelper.SeedFeedbackAsync(
                db,
                tenant,
                message,
                rating: i < 15 ? "up" : "down",
                createdAt: oldStamp.AddMinutes(i));
        }

        var currentStamp = DateTime.UtcNow.AddDays(-2);
        for (var i = 0; i < 10; i++)
        {
            var message = await SeedHelper.SeedChatMessageAsync(
                db,
                session,
                role: "assistant",
                content: $"safe-current-{i}",
                createdAt: currentStamp.AddMinutes(i));
            await SeedHelper.SeedFeedbackAsync(
                db,
                tenant,
                message,
                rating: i < 7 ? "up" : "down",
                createdAt: currentStamp.AddMinutes(i));
        }

        await job.RunAsync();

        var alerts = db.DriftAlerts.Where(a => a.TenantId == tenant.Id).ToList();
        alerts.Should().BeEmpty();
    }
}
