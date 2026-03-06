using Api.Admin.Models.Requests;
using Api.Admin.Models.Responses;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/evals")]
[Authorize]
public class EvalsController(AppDbContext db) : ControllerBase
{
    private const double RelativeThresholdFaithfulness = 0.05;
    private const double RelativeThresholdRelevancy = 0.05;
    private const double RelativeThresholdPrecision = 0.08;
    private const double RelativeThresholdRecall = 0.08;
    private const double AbsoluteThresholdHallucination = 0.03;

    [HttpGet("global")]
    public async Task<ActionResult<List<GlobalEvalHeatmapRowResponse>>> Global([FromQuery] int maxRows = 200)
    {
        if (maxRows <= 0)
            maxRows = 200;

        var latestRuns = await db.EvalRuns
            .Where(r => r.Status == "completed")
            .Include(r => r.Tenant)
            .OrderByDescending(r => r.FinishedAt ?? r.StartedAt)
            .ToListAsync();

        var latestPerTenant = latestRuns
            .GroupBy(r => r.TenantId)
            .Select(g => g.First())
            .Take(maxRows)
            .ToList();

        if (latestPerTenant.Count == 0)
            return Ok(new List<GlobalEvalHeatmapRowResponse>());

        var runIds = latestPerTenant.Select(r => r.Id).ToList();

        var metrics = await db.EvalResults
            .Where(r => runIds.Contains(r.RunId))
            .GroupBy(r => r.RunId)
            .Select(g => new
            {
                RunId = g.Key,
                Faithfulness = g.Average(x => x.Faithfulness),
                AnswerRelevancy = g.Average(x => x.AnswerRelevancy),
                ContextPrecision = g.Average(x => x.ContextPrecision),
                ContextRecall = g.Average(x => x.ContextRecall),
                HallucinationScore = g.Average(x => x.HallucinationScore),
            })
            .ToListAsync();

        var metricsByRunId = metrics.ToDictionary(x => x.RunId, x => x);

        var rows = latestPerTenant
            .Select(run =>
            {
                metricsByRunId.TryGetValue(run.Id, out var m);
                return new GlobalEvalHeatmapRowResponse(
                    run.TenantId,
                    run.Tenant.Slug,
                    run.Tenant.Name,
                    run.Id,
                    run.FinishedAt,
                    m?.Faithfulness,
                    m?.AnswerRelevancy,
                    m?.ContextPrecision,
                    m?.ContextRecall,
                    m?.HallucinationScore
                );
            })
            .OrderByDescending(r => r.FinishedAt)
            .ToList();

        return Ok(rows);
    }

    [HttpPost("baseline")]
    public async Task<ActionResult<EvalBaselineResponse>> PinBaseline([FromBody] PinEvalBaselineRequest request)
    {
        var run = await db.EvalRuns.FirstOrDefaultAsync(r => r.Id == request.RunId);
        if (run is null)
            return NotFound(new { error = "run_not_found" });

        var baseline = await db.EvalBaselines
            .FirstOrDefaultAsync(b => b.TenantId == run.TenantId);

        if (baseline is null)
        {
            baseline = new EvalBaseline
            {
                Id = Guid.NewGuid(),
                TenantId = run.TenantId,
                RunId = run.Id,
                SetAt = DateTime.UtcNow,
                SetBy = string.IsNullOrWhiteSpace(request.SetBy) ? "admin" : request.SetBy!.Trim(),
            };
            db.EvalBaselines.Add(baseline);
        }
        else
        {
            baseline.RunId = run.Id;
            baseline.SetAt = DateTime.UtcNow;
            baseline.SetBy = string.IsNullOrWhiteSpace(request.SetBy) ? "admin" : request.SetBy!.Trim();
        }

        await db.SaveChangesAsync();
        return Ok(new EvalBaselineResponse(
            baseline.Id,
            baseline.TenantId,
            baseline.RunId,
            baseline.SetAt,
            baseline.SetBy));
    }

    [HttpGet("baseline")]
    public async Task<ActionResult<EvalBaselineResponse>> GetBaseline([FromQuery] Guid tenantId)
    {
        var baseline = await db.EvalBaselines.FirstOrDefaultAsync(b => b.TenantId == tenantId);
        if (baseline is null)
            return NotFound(new { error = "baseline_not_found" });

        return Ok(new EvalBaselineResponse(
            baseline.Id,
            baseline.TenantId,
            baseline.RunId,
            baseline.SetAt,
            baseline.SetBy));
    }

    [HttpPost("regression-check")]
    public async Task<ActionResult<EvalRegressionCheckResponse>> RegressionCheck([FromBody] EvalRegressionCheckRequest request)
    {
        var baseline = await db.EvalBaselines.FirstOrDefaultAsync(b => b.TenantId == request.TenantId);
        if (baseline is null)
            return NotFound(new { error = "baseline_not_set" });

        var baselineRun = await db.EvalRuns
            .FirstOrDefaultAsync(r => r.Id == baseline.RunId && r.TenantId == request.TenantId);
        var currentRun = await db.EvalRuns
            .FirstOrDefaultAsync(r => r.Id == request.RunId && r.TenantId == request.TenantId);

        if (baselineRun is null || currentRun is null)
            return NotFound(new { error = "run_not_found" });

        var baselineMetrics = await AggregateMetricsAsync(baselineRun.Id);
        var currentMetrics = await AggregateMetricsAsync(currentRun.Id);

        var checks = new List<EvalRegressionMetricResponse>
        {
            RelativeDropCheck("faithfulness", baselineMetrics.Faithfulness, currentMetrics.Faithfulness, RelativeThresholdFaithfulness),
            RelativeDropCheck("answer_relevancy", baselineMetrics.AnswerRelevancy, currentMetrics.AnswerRelevancy, RelativeThresholdRelevancy),
            RelativeDropCheck("context_precision", baselineMetrics.ContextPrecision, currentMetrics.ContextPrecision, RelativeThresholdPrecision),
            RelativeDropCheck("context_recall", baselineMetrics.ContextRecall, currentMetrics.ContextRecall, RelativeThresholdRecall),
            AbsoluteIncreaseCheck("hallucination_rate", baselineMetrics.HallucinationScore, currentMetrics.HallucinationScore, AbsoluteThresholdHallucination),
        };

        return Ok(new EvalRegressionCheckResponse(
            request.TenantId,
            baselineRun.Id,
            currentRun.Id,
            checks.Any(c => c.Failed),
            checks));
    }

    private async Task<AggregatedMetrics> AggregateMetricsAsync(Guid runId)
    {
        var rows = await db.EvalResults
            .Where(r => r.RunId == runId)
            .ToListAsync();

        if (rows.Count == 0)
            return new AggregatedMetrics(0, 0, 0, 0, 0);

        double sumFaithfulness = 0;
        int countFaithfulness = 0;
        double sumRelevancy = 0;
        int countRelevancy = 0;
        double sumPrecision = 0;
        int countPrecision = 0;
        double sumRecall = 0;
        int countRecall = 0;
        double sumHallucination = 0;
        int countHallucination = 0;

        foreach (var row in rows)
        {
            if (row.Faithfulness.HasValue)
            {
                sumFaithfulness += row.Faithfulness.Value;
                countFaithfulness++;
            }

            if (row.AnswerRelevancy.HasValue)
            {
                sumRelevancy += row.AnswerRelevancy.Value;
                countRelevancy++;
            }

            if (row.ContextPrecision.HasValue)
            {
                sumPrecision += row.ContextPrecision.Value;
                countPrecision++;
            }

            if (row.ContextRecall.HasValue)
            {
                sumRecall += row.ContextRecall.Value;
                countRecall++;
            }

            if (row.HallucinationScore.HasValue)
            {
                sumHallucination += row.HallucinationScore.Value;
                countHallucination++;
            }
        }

        return new AggregatedMetrics(
            countFaithfulness > 0 ? sumFaithfulness / countFaithfulness : 0,
            countRelevancy > 0 ? sumRelevancy / countRelevancy : 0,
            countPrecision > 0 ? sumPrecision / countPrecision : 0,
            countRecall > 0 ? sumRecall / countRecall : 0,
            countHallucination > 0 ? sumHallucination / countHallucination : 0);
    }

    private static EvalRegressionMetricResponse RelativeDropCheck(
        string metric,
        double baseline,
        double current,
        double threshold)
    {
        var drop = baseline <= 0 ? 0 : Math.Max(0, (baseline - current) / baseline);
        return new EvalRegressionMetricResponse(
            metric,
            baseline,
            current,
            threshold,
            "relative_drop",
            drop > threshold);
    }

    private static EvalRegressionMetricResponse AbsoluteIncreaseCheck(
        string metric,
        double baseline,
        double current,
        double threshold)
    {
        var increase = Math.Max(0, current - baseline);
        return new EvalRegressionMetricResponse(
            metric,
            baseline,
            current,
            threshold,
            "absolute_increase",
            increase > threshold);
    }

    private sealed record AggregatedMetrics(
        double Faithfulness,
        double AnswerRelevancy,
        double ContextPrecision,
        double ContextRecall,
        double HallucinationScore);
}
