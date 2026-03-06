using Api.Portal.Services;

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
