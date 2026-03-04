namespace Api.Public.Services;

public record PublicRagSource(string File, int? Page, int? Offset, float? RelevanceScore, string BriefContent);
public record PublicRagResult(string Answer, List<PublicRagSource> Sources);

public interface IPublicRagClient
{
    Task<PublicRagResult> QueryAsync(string tenantSlug, string query, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamQueryAsync(string tenantSlug, string query, CancellationToken ct = default);
}
