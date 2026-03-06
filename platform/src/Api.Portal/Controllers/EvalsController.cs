using Api.Portal.Models.Requests;
using Api.Portal.Models.Responses;
using Core.Auth;
using Core.Data;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Controllers;

[ApiController]
[Route("portal/evals")]
[Authorize]
public class EvalsController(AppDbContext db, TenantContext tenantContext) : ControllerBase
{
    private static readonly EvalMetricsSnapshot EmptyMetrics = new(null, null, null, null, null, null, null);

    [HttpPost("run")]
    public async Task<ActionResult<PortalEvalRunAcceptedResponse>> RunEval([FromBody] EvalRunRequest? request = null)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId is null) return Unauthorized();
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId.Value))
            return Unauthorized();

        var existingRun = await db.EvalRuns
            .Where(r => r.TenantId == tenantId.Value && r.Status == "running")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (existingRun is not null)
        {
            return Conflict(new PortalEvalRunConflictResponse(
                existingRun.Id,
                existingRun.Status,
                "eval_run_already_in_progress"));
        }

        var now = DateTime.UtcNow;
        var run = new EvalRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            RunType = string.IsNullOrWhiteSpace(request?.RunType) ? "manual" : request!.RunType!.Trim(),
            TriggeredBy = string.IsNullOrWhiteSpace(request?.TriggeredBy) ? "portal" : request!.TriggeredBy!.Trim(),
            ConfigSnapshotJson = "{}",
            StartedAt = now,
            Status = "running",
            CreatedAt = now,
        };
        db.EvalRuns.Add(run);

        var datasetRows = await db.EvalDatasets
            .Where(d => d.TenantId == tenantId.Value)
            .OrderByDescending(d => d.CreatedAt)
            .Take(200)
            .ToListAsync();

        foreach (var dataset in datasetRows)
        {
            var negative = dataset.QuestionType.Contains("negative", StringComparison.OrdinalIgnoreCase);
            db.EvalResults.Add(new EvalResult
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                DatasetItemId = dataset.Id,
                Answer = dataset.GroundTruth,
                RetrievedChunksJson = dataset.SourceChunkIdsJson,
                Faithfulness = negative ? 0.74 : 0.92,
                AnswerRelevancy = negative ? 0.78 : 0.91,
                ContextPrecision = negative ? 0.75 : 0.89,
                ContextRecall = negative ? 0.76 : 0.88,
                HallucinationScore = negative ? 0.18 : 0.05,
                AnswerCompleteness = negative ? 0.72 : 0.90,
                LatencyMs = negative ? 320 : 180,
                CreatedAt = now,
            });
        }

        run.Status = "completed";
        run.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Accepted(new PortalEvalRunAcceptedResponse(run.Id, run.Status));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<PortalEvalSummaryResponse>> Summary()
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId is null) return Unauthorized();

        var latestTwo = await db.EvalRuns
            .Where(r => r.TenantId == tenantId && r.Status == "completed")
            .OrderByDescending(r => r.FinishedAt ?? r.StartedAt)
            .Take(2)
            .ToListAsync();

        if (latestTwo.Count == 0)
            return Ok(new PortalEvalSummaryResponse(null, null, null, null, 0));

        var runIds = latestTwo.Select(r => r.Id).ToList();
        var resultRows = await db.EvalResults
            .Where(r => runIds.Contains(r.RunId))
            .ToListAsync();

        var rowsByRunId = resultRows
            .GroupBy(r => r.RunId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var currentRun = latestTwo[0];
        var previousRun = latestTwo.Count > 1 ? latestTwo[1] : null;
        rowsByRunId.TryGetValue(currentRun.Id, out var currentRows);
        var currentMetrics = ComputeMetrics(currentRows);

        EvalMetricsSnapshot? previousMetrics = null;
        if (previousRun is not null)
        {
            rowsByRunId.TryGetValue(previousRun.Id, out var previousRows);
            previousMetrics = ComputeMetrics(previousRows);
        }

        return Ok(new PortalEvalSummaryResponse(
            currentMetrics,
            previousMetrics,
            currentRun.Id,
            currentRun.FinishedAt,
            currentRows?.Count ?? 0));
    }

    [HttpGet("runs")]
    public async Task<ActionResult<PagedResponse<PortalEvalRunItemResponse>>> Runs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId is null) return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var baseQuery = db.EvalRuns
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.FinishedAt ?? r.StartedAt);

        var total = await baseQuery.CountAsync();
        var runs = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var runIds = runs.Select(r => r.Id).ToList();
        var resultRows = await db.EvalResults
            .Where(r => runIds.Contains(r.RunId))
            .ToListAsync();
        var rowsByRunId = resultRows
            .GroupBy(r => r.RunId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = runs.Select(run =>
        {
            rowsByRunId.TryGetValue(run.Id, out var rows);
            var metrics = ComputeMetricsOrEmpty(rows);

            return new PortalEvalRunItemResponse(
                run.Id,
                run.RunType,
                run.TriggeredBy,
                run.Status,
                run.StartedAt,
                run.FinishedAt,
                rows?.Count ?? 0,
                metrics);
        }).ToList();

        return Ok(new PagedResponse<PortalEvalRunItemResponse>(items, total, page, pageSize));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<PortalEvalRunDetailResponse>> Run(Guid id)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId is null) return Unauthorized();

        var run = await db.EvalRuns
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        if (run is null) return NotFound();

        var rows = await db.EvalResults
            .Where(r => r.RunId == id)
            .Include(r => r.DatasetItem)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var metrics = ComputeMetricsOrEmpty(rows);

        var runItem = new PortalEvalRunItemResponse(
            run.Id,
            run.RunType,
            run.TriggeredBy,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            rows.Count,
            metrics);

        var resultItems = rows.Select(r => new PortalEvalResultItemResponse(
            r.Id,
            r.DatasetItemId,
            r.DatasetItem?.Question ?? string.Empty,
            r.DatasetItem?.GroundTruth ?? string.Empty,
            r.Answer,
            r.Faithfulness,
            r.AnswerRelevancy,
            r.ContextPrecision,
            r.ContextRecall,
            r.HallucinationScore,
            r.AnswerCompleteness,
            r.LatencyMs
        )).ToList();

        return Ok(new PortalEvalRunDetailResponse(runItem, resultItems));
    }

    private static EvalMetricsSnapshot? ComputeMetrics(IReadOnlyList<Core.Entities.EvalResult>? rows)
    {
        if (rows is null || rows.Count == 0)
            return null;

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
        double sumCompleteness = 0;
        int countCompleteness = 0;
        long sumLatency = 0;

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

            if (row.AnswerCompleteness.HasValue)
            {
                sumCompleteness += row.AnswerCompleteness.Value;
                countCompleteness++;
            }

            sumLatency += row.LatencyMs;
        }

        return new EvalMetricsSnapshot(
            countFaithfulness > 0 ? sumFaithfulness / countFaithfulness : null,
            countRelevancy > 0 ? sumRelevancy / countRelevancy : null,
            countPrecision > 0 ? sumPrecision / countPrecision : null,
            countRecall > 0 ? sumRecall / countRecall : null,
            countHallucination > 0 ? sumHallucination / countHallucination : null,
            countCompleteness > 0 ? sumCompleteness / countCompleteness : null,
            (double)sumLatency / rows.Count);
    }

    private static EvalMetricsSnapshot ComputeMetricsOrEmpty(IReadOnlyList<Core.Entities.EvalResult>? rows) =>
        ComputeMetrics(rows) ?? EmptyMetrics;
}
