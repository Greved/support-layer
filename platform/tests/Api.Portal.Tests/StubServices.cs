using Api.Portal.Services;
using Core.Entities;
using Core.Evals;

namespace Api.Portal.Tests;

public class StubRagClient : IRagClient
{
    public List<(string TenantSlug, string TriggerReason)> TriggeredEvalRuns { get; } = [];

    public Task<RagQueryResult> QueryAsync(string tenantSlug, string query)
        => Task.FromResult(new RagQueryResult("Stub answer.", []));

    public Task<RagIngestResult> IngestAsync(string tenantSlug, string documentId, string fileName, byte[] fileBytes, string contentType)
        => Task.FromResult(new RagIngestResult(3, documentId));

    public Task<RagEvalTriggerResult> TriggerEvalRunAsync(string tenantSlug, string triggerReason)
    {
        TriggeredEvalRuns.Add((tenantSlug, triggerReason));
        return Task.FromResult(new RagEvalTriggerResult("accepted", triggerReason));
    }
}

public class StubEmailService : IEmailService
{
    public List<(string Email, string Link)> SentResets { get; } = [];

    public Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        SentResets.Add((toEmail, resetLink));
        return Task.CompletedTask;
    }

    public Task SendInviteAsync(string toEmail, string inviteLink, string tenantName)
        => Task.CompletedTask;
}

public class StubCleanAntivirusScanner : IAntivirusScanner
{
    public Task<AntivirusScanResult> ScanAsync(Stream stream, CancellationToken ct = default)
        => Task.FromResult(new AntivirusScanResult(AntivirusScanStatus.Clean));
}

public class StubInfectedAntivirusScanner(string signature = "Win.Test.EICAR_HDB-1") : IAntivirusScanner
{
    public Task<AntivirusScanResult> ScanAsync(Stream stream, CancellationToken ct = default)
        => Task.FromResult(new AntivirusScanResult(AntivirusScanStatus.Infected, Signature: signature));
}

public class StubUnavailableAntivirusScanner : IAntivirusScanner
{
    public Task<AntivirusScanResult> ScanAsync(Stream stream, CancellationToken ct = default)
        => Task.FromResult(new AntivirusScanResult(AntivirusScanStatus.Unavailable, Details: "scanner_unreachable"));
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
