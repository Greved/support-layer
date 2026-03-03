namespace Api.Portal.Services;

public interface IRagClient
{
    Task<RagQueryResult> QueryAsync(string tenantSlug, string query);
    Task<RagIngestResult> IngestAsync(string tenantSlug, string documentId, string fileName, byte[] fileBytes, string contentType);
}

public record RagQueryResult(string Answer, List<RagSource> Sources);
public record RagSource(string File, int? Page, int? Offset, float? RelevanceScore, string BriefContent);
public record RagIngestResult(int ChunksWritten, string DocumentId);
