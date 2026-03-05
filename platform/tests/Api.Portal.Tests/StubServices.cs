using Api.Portal.Services;

namespace Api.Portal.Tests;

public class StubRagClient : IRagClient
{
    public Task<RagQueryResult> QueryAsync(string tenantSlug, string query)
        => Task.FromResult(new RagQueryResult("Stub answer.", []));

    public Task<RagIngestResult> IngestAsync(string tenantSlug, string documentId, string fileName, byte[] fileBytes, string contentType)
        => Task.FromResult(new RagIngestResult(3, documentId));
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
