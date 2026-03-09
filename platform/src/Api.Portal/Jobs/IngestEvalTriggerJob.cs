using Api.Portal.Constants;
using Api.Portal.Services;
using Core.Data;
using Core.Entities;
using Core.Evals;
using Microsoft.EntityFrameworkCore;

namespace Api.Portal.Jobs;

public class IngestEvalTriggerJob(
    AppDbContext db,
    IRagClient ragClient,
    IEvalScoringService evalScoringService,
    ILogger<IngestEvalTriggerJob> logger)
{
    public async Task RunAsync(Guid documentId)
    {
        var document = await db.Documents
            .Include(d => d.Tenant)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document is null)
        {
            logger.LogWarning("IngestEvalTriggerJob: document {DocumentId} not found", documentId);
            return;
        }

        if (!document.IsActive || document.Status != DocumentStatus.Ready)
        {
            logger.LogInformation(
                "IngestEvalTriggerJob skipped for document {DocumentId}: active={IsActive} status={Status}",
                document.Id,
                document.IsActive,
                document.Status);
            return;
        }

        var existingRun = await db.EvalRuns
            .Where(r => r.TenantId == document.TenantId && r.Status == "running")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (existingRun is not null)
        {
            logger.LogInformation(
                "IngestEvalTriggerJob skipped because eval run {RunId} is already running for tenant {TenantId}",
                existingRun.Id,
                document.TenantId);
            return;
        }

        var now = DateTime.UtcNow;
        EvalRun? run = null;
        try
        {
            var datasetRows = await db.EvalDatasets
                .Where(d => d.TenantId == document.TenantId)
                .OrderByDescending(d => d.CreatedAt)
                .Take(200)
                .ToListAsync();

            var tenantConfigs = await db.TenantConfigs
                .Where(c => c.TenantId == document.TenantId)
                .ToListAsync();

            run = new EvalRun
            {
                Id = Guid.NewGuid(),
                TenantId = document.TenantId,
                RunType = "ingest",
                TriggeredBy = "system",
                ConfigSnapshotJson = EvalContextSnapshotBuilder.BuildRunSnapshot(
                    document.TenantId,
                    document.Tenant.Slug,
                    "ingest",
                    "system",
                    now,
                    datasetRows,
                    tenantConfigs,
                    source: "ingestion_job",
                    triggerContext: new
                    {
                        documentId = document.Id,
                        fileName = document.FileName,
                        status = document.Status,
                        chunkCount = document.ChunkCount,
                    }),
                StartedAt = now,
                Status = "running",
                CreatedAt = now,
            };
            db.EvalRuns.Add(run);
            await db.SaveChangesAsync();

            await ragClient.TriggerEvalRunAsync(
                document.Tenant.Slug,
                $"ingest_complete:{document.Id}");

            var scoring = await evalScoringService.ScoreAsync(
                document.Tenant.Slug,
                run.Id.ToString("N"),
                datasetRows);

            run.ConfigSnapshotJson = EvalContextSnapshotBuilder.BuildRunSnapshot(
                document.TenantId,
                document.Tenant.Slug,
                "ingest",
                "system",
                now,
                datasetRows,
                tenantConfigs,
                source: "ingestion_job",
                triggerContext: new
                {
                    documentId = document.Id,
                    fileName = document.FileName,
                    status = document.Status,
                    chunkCount = document.ChunkCount,
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

            logger.LogInformation(
                "IngestEvalTriggerJob completed runId={RunId} tenantId={TenantId} datasetRows={DatasetRows}",
                run.Id,
                document.TenantId,
                datasetRows.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IngestEvalTriggerJob failed for document {DocumentId}", document.Id);
            if (run is not null)
            {
                run.Status = "failed";
                run.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
    }
}
