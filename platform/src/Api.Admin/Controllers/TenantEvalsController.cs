using Api.Admin.Models.Requests;
using Api.Admin.Models.Responses;
using Core.Data;
using Core.Entities;
using Core.Evals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Controllers;

[ApiController]
[Route("admin/tenants/{tenantId:guid}/evals")]
[Authorize]
public class TenantEvalsController(AppDbContext db, IEvalScoringService evalScoringService) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<EvalDatasetGenerateResponse>> Generate(Guid tenantId, [FromQuery] int maxDocuments = 50)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound();

        maxDocuments = Math.Clamp(maxDocuments, 1, 200);
        var datasetVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        var oldSyntheticRows = await db.EvalDatasets
            .Where(d => d.TenantId == tenantId && EF.Functions.Like(d.QuestionType, "synthetic_%"))
            .ToListAsync();
        if (oldSyntheticRows.Count > 0)
            db.EvalDatasets.RemoveRange(oldSyntheticRows);

        var documents = await db.Documents
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(maxDocuments)
            .ToListAsync();

        var generated = new List<EvalDataset>();
        foreach (var doc in documents)
        {
            var sourceChunkIdsJson = $"[\"doc:{doc.Id}\"]";
            generated.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Question = $"What topics are covered in {doc.FileName}?",
                GroundTruth = $"The document {doc.FileName} is available with status '{doc.Status}'.",
                SourceChunkIdsJson = sourceChunkIdsJson,
                QuestionType = "synthetic_simple",
                DatasetVersion = datasetVersion,
                CreatedAt = DateTime.UtcNow,
            });
            generated.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Question = $"How does {doc.FileName} connect to tenant support operations?",
                GroundTruth = $"Use {doc.FileName} with related tenant documents to answer multi-step support questions.",
                SourceChunkIdsJson = sourceChunkIdsJson,
                QuestionType = "synthetic_multihop",
                DatasetVersion = datasetVersion,
                CreatedAt = DateTime.UtcNow,
            });
            generated.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Question = $"What should happen if {doc.FileName} lacks details for a user edge case?",
                GroundTruth = $"Flag low confidence and request escalation or updated knowledge for {doc.FileName}.",
                SourceChunkIdsJson = sourceChunkIdsJson,
                QuestionType = "synthetic_adversarial",
                DatasetVersion = datasetVersion,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (generated.Count == 0)
        {
            generated.Add(new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Question = "What information is currently missing from the tenant knowledge base?",
                GroundTruth = "No active documents are available; ingest documents before evaluation.",
                SourceChunkIdsJson = "[]",
                QuestionType = "synthetic_adversarial",
                DatasetVersion = datasetVersion,
                CreatedAt = DateTime.UtcNow,
            });
        }

        db.EvalDatasets.AddRange(generated);
        await db.SaveChangesAsync();

        return Accepted(new EvalDatasetGenerateResponse(tenantId, datasetVersion, generated.Count, "completed"));
    }

    [HttpPost("run")]
    public async Task<ActionResult<EvalRunAcceptedResponse>> Run(Guid tenantId, [FromBody] TriggerEvalRunRequest? request = null)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant is null)
            return NotFound();

        var existingRun = await db.EvalRuns
            .Where(r => r.TenantId == tenantId && r.Status == "running")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (existingRun is not null)
        {
            return Conflict(new EvalRunConflictResponse(
                existingRun.Id,
                existingRun.Status,
                "eval_run_already_in_progress"));
        }

        var datasetRows = await db.EvalDatasets
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(200)
            .ToListAsync();

        var tenantConfigs = await db.TenantConfigs
            .Where(c => c.TenantId == tenantId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var runType = string.IsNullOrWhiteSpace(request?.RunType) ? "manual" : request!.RunType!.Trim();
        var triggeredBy = string.IsNullOrWhiteSpace(request?.TriggeredBy) ? "admin" : request!.TriggeredBy!.Trim();

        var run = new EvalRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RunType = runType,
            TriggeredBy = triggeredBy,
            ConfigSnapshotJson = EvalContextSnapshotBuilder.BuildRunSnapshot(
                tenant.Id,
                tenant.Slug,
                runType,
                triggeredBy,
                now,
                datasetRows,
                tenantConfigs,
                source: "admin_eval_run"),
            StartedAt = now,
            Status = "running",
            CreatedAt = now,
        };

        db.EvalRuns.Add(run);
        var scoring = await evalScoringService.ScoreAsync(
            tenant.Slug,
            run.Id.ToString("N"),
            datasetRows,
            HttpContext.RequestAborted);

        run.ConfigSnapshotJson = EvalContextSnapshotBuilder.BuildRunSnapshot(
            tenant.Id,
            tenant.Slug,
            runType,
            triggeredBy,
            now,
            datasetRows,
            tenantConfigs,
            source: "admin_eval_run",
            triggerContext: new
            {
                scoringEngine = scoring.Engine,
                usedFallback = scoring.UsedFallback,
                diagnostics = scoring.Diagnostics,
                timings = scoring.Timings,
                scoredRows = scoring.Rows.Count,
            });

        for (var i = 0; i < datasetRows.Count; i++)
        {
            var dataset = datasetRows[i];
            var scored = i < scoring.Rows.Count
                ? scoring.Rows[i]
                : new EvalScoredRow(
                    dataset.Question,
                    dataset.GroundTruth,
                    dataset.GroundTruth,
                    EvalContextSnapshotBuilder.ParseSourceChunkIds(dataset.SourceChunkIdsJson),
                    0,
                    0,
                    0,
                    0,
                    1,
                    0,
                    180);

            var sourceChunkIds = EvalContextSnapshotBuilder.ParseSourceChunkIds(dataset.SourceChunkIdsJson);
            var retrievedChunks = scored.RetrievedChunks.Count > 0 ? scored.RetrievedChunks : sourceChunkIds;
            var retrievedChunksJson = EvalContextSnapshotBuilder.BuildRetrievedChunksSnapshot(retrievedChunks);

            db.EvalResults.Add(new EvalResult
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                DatasetItemId = dataset.Id,
                Answer = scored.Answer,
                RetrievedChunksJson = retrievedChunksJson,
                ContextSnapshotJson = EvalContextSnapshotBuilder.BuildResultSnapshot(
                    dataset,
                    sourceChunkIds,
                    retrievedChunks,
                    scored.Answer,
                    scored.LatencyMs,
                    scored.Faithfulness,
                    scored.AnswerRelevancy,
                    scored.ContextPrecision,
                    scored.ContextRecall,
                    scored.HallucinationScore,
                    scored.AnswerCompleteness),
                Faithfulness = scored.Faithfulness,
                AnswerRelevancy = scored.AnswerRelevancy,
                ContextPrecision = scored.ContextPrecision,
                ContextRecall = scored.ContextRecall,
                HallucinationScore = scored.HallucinationScore,
                AnswerCompleteness = scored.AnswerCompleteness,
                LatencyMs = scored.LatencyMs,
                CreatedAt = now,
            });
        }

        run.Status = "completed";
        run.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Accepted(new EvalRunAcceptedResponse(run.Id, run.Status));
    }

    [HttpGet("runs")]
    public async Task<ActionResult<List<EvalRunSummaryResponse>>> ListRuns(Guid tenantId)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound();

        var runs = await db.EvalRuns
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync();

        if (runs.Count == 0)
            return Ok(new List<EvalRunSummaryResponse>());

        var runIds = runs.Select(r => r.Id).ToList();
        var resultRows = await db.EvalResults
            .Where(r => runIds.Contains(r.RunId))
            .ToListAsync();

        var aggregatesByRunId = resultRows
            .GroupBy(r => r.RunId)
            .ToDictionary(g => g.Key, g => AggregateRows(g));

        var response = runs.Select(run =>
        {
            var aggregate = aggregatesByRunId.TryGetValue(run.Id, out var value)
                ? value
                : EvalRunAggregates.Empty;

            return new EvalRunSummaryResponse(
                run.Id,
                run.TenantId,
                run.RunType,
                run.TriggeredBy,
                run.Status,
                run.StartedAt,
                run.FinishedAt,
                aggregate.ResultCount,
                aggregate.Faithfulness,
                aggregate.AnswerRelevancy,
                aggregate.ContextPrecision,
                aggregate.ContextRecall,
                aggregate.HallucinationScore,
                aggregate.AnswerCompleteness,
                aggregate.AvgLatencyMs);
        }).ToList();

        return Ok(response);
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<EvalRunDetailResponse>> GetRun(Guid tenantId, Guid runId)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound();

        var run = await db.EvalRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == tenantId);

        if (run is null)
            return NotFound();

        var resultRows = await db.EvalResults
            .Where(r => r.RunId == runId)
            .Include(r => r.DatasetItem)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var aggregate = AggregateRows(resultRows);

        var runSummary = new EvalRunSummaryResponse(
            run.Id,
            run.TenantId,
            run.RunType,
            run.TriggeredBy,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            aggregate.ResultCount,
            aggregate.Faithfulness,
            aggregate.AnswerRelevancy,
            aggregate.ContextPrecision,
            aggregate.ContextRecall,
            aggregate.HallucinationScore,
            aggregate.AnswerCompleteness,
            aggregate.AvgLatencyMs,
            run.ConfigSnapshotJson);

        var results = resultRows
            .Select(r => new EvalResultDetailResponse(
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
                r.LatencyMs,
                r.RetrievedChunksJson,
                r.ContextSnapshotJson
            ))
            .ToList();

        return Ok(new EvalRunDetailResponse(runSummary, results));
    }

    private static EvalRunAggregates AggregateRows(IEnumerable<EvalResult> rows)
    {
        int resultCount = 0;
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
            resultCount++;

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

        if (resultCount == 0)
            return EvalRunAggregates.Empty;

        return new EvalRunAggregates(
            resultCount,
            countFaithfulness > 0 ? sumFaithfulness / countFaithfulness : null,
            countRelevancy > 0 ? sumRelevancy / countRelevancy : null,
            countPrecision > 0 ? sumPrecision / countPrecision : null,
            countRecall > 0 ? sumRecall / countRecall : null,
            countHallucination > 0 ? sumHallucination / countHallucination : null,
            countCompleteness > 0 ? sumCompleteness / countCompleteness : null,
            (double)sumLatency / resultCount);
    }

    private sealed record EvalRunAggregates(
        int ResultCount,
        double? Faithfulness,
        double? AnswerRelevancy,
        double? ContextPrecision,
        double? ContextRecall,
        double? HallucinationScore,
        double? AnswerCompleteness,
        double? AvgLatencyMs)
    {
        public static EvalRunAggregates Empty { get; } = new(0, null, null, null, null, null, null, null);
    }
}
