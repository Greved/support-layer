using Core.Data;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Jobs;

public class FeedbackDriftDetectionJob(AppDbContext db, ILogger<FeedbackDriftDetectionJob> logger)
{
    private const string Signal = "thumbs_up_rate_drop";
    private const double DropThreshold = 0.10;
    private const int BaselineMinSamples = 10;
    private const int CurrentMinSamples = 3;

    public async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var windowEnd = now.Date;
        var currentStart = windowEnd.AddDays(-7);
        var baselineStart = windowEnd.AddDays(-30);

        var aggregates = await db.ChatMessageFeedback
            .Where(f => f.CreatedAt >= baselineStart && f.CreatedAt < windowEnd)
            .GroupBy(f => f.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                BaselineTotal = g.Count(),
                BaselineUp = g.Count(f => f.Rating == "up"),
                CurrentTotal = g.Count(f => f.CreatedAt >= currentStart),
                CurrentUp = g.Count(f => f.CreatedAt >= currentStart && f.Rating == "up"),
            })
            .ToListAsync();

        var created = 0;
        foreach (var tenant in aggregates)
        {
            if (tenant.BaselineTotal < BaselineMinSamples || tenant.CurrentTotal < CurrentMinSamples)
                continue;

            var baselineRate = tenant.BaselineUp / (double)tenant.BaselineTotal;
            var currentRate = tenant.CurrentUp / (double)tenant.CurrentTotal;
            var drop = baselineRate - currentRate;

            if (drop <= DropThreshold)
                continue;

            var alreadyExists = await db.DriftAlerts.AnyAsync(a =>
                a.TenantId == tenant.TenantId
                && a.Signal == Signal
                && a.WindowStartUtc == currentStart
                && a.WindowEndUtc == windowEnd);

            if (alreadyExists)
                continue;

            var reason = $"Thumbs-up rate dropped from {baselineRate:P1} baseline to {currentRate:P1} in last 7 days.";
            db.DriftAlerts.Add(new DriftAlert
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId,
                Signal = Signal,
                BaselineRate = baselineRate,
                CurrentRate = currentRate,
                DropAmount = drop,
                Threshold = DropThreshold,
                Reason = reason,
                WindowStartUtc = currentStart,
                WindowEndUtc = windowEnd,
                CreatedAt = now,
            });
            created++;

            logger.LogWarning(
                "Feedback drift alert created tenantId={TenantId} baselineRate={BaselineRate:F4} currentRate={CurrentRate:F4} drop={Drop:F4}",
                tenant.TenantId,
                baselineRate,
                currentRate,
                drop);
        }

        if (created > 0)
            await db.SaveChangesAsync();

        logger.LogInformation(
            "Feedback drift detection completed tenantsEvaluated={TenantCount} alertsCreated={Created}",
            aggregates.Count,
            created);
    }
}
