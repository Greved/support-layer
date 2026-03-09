using Api.Admin.Models.Responses;
using Api.Admin.Services;
using Core.Entities;
using Core.Evals;

namespace Api.Admin.Tests;

public class StubInfraHealthService : IInfraHealthService
{
    public Task<InfraHealthResponse> CheckAllAsync(CancellationToken ct = default)
    {
        var services = new List<ServiceHealth>
        {
            new("rag-core", "healthy", LatencyMs: 5),
            new("qdrant", "healthy", LatencyMs: 3),
            new("postgres", "healthy", LatencyMs: 1),
        };
        return Task.FromResult(new InfraHealthResponse("healthy", services));
    }
}

public class StubQdrantAdminService : IQdrantAdminService
{
    public Task<IReadOnlyList<CollectionInfoResponse>> ListCollectionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CollectionInfoResponse> result =
        [
            new CollectionInfoResponse("tenant_acme", "acme", 1000, 500),
        ];
        return Task.FromResult(result);
    }

    public Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class StubEvalScoringService : IEvalScoringService
{
    public Task<EvalScoringBatchResult> ScoreAsync(
        string tenantSlug,
        string runId,
        IReadOnlyList<EvalDataset> datasetRows,
        CancellationToken cancellationToken = default)
    {
        var rows = datasetRows.Select(dataset =>
        {
            var difficult = dataset.QuestionType.Contains("negative", StringComparison.OrdinalIgnoreCase)
                || dataset.QuestionType.Contains("adversarial", StringComparison.OrdinalIgnoreCase);
            var retrieved = EvalContextSnapshotBuilder.ParseSourceChunkIds(dataset.SourceChunkIdsJson);
            var fallbackRetrieved = retrieved.Count > 0 ? retrieved : [dataset.GroundTruth];

            return new EvalScoredRow(
                dataset.Question,
                dataset.GroundTruth,
                dataset.GroundTruth,
                fallbackRetrieved,
                difficult ? 0.74 : 0.92,
                difficult ? 0.78 : 0.91,
                difficult ? 0.75 : 0.89,
                difficult ? 0.76 : 0.88,
                difficult ? 0.18 : 0.05,
                difficult ? 0.72 : 0.90,
                difficult ? 320 : 180);
        }).ToList();

        return Task.FromResult(new EvalScoringBatchResult(
            rows,
            new Dictionary<string, double>(StringComparer.Ordinal),
            "test-stub",
            false,
            null));
    }
}
