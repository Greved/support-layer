using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Api.Portal.Services;

public class RagClient(HttpClient http, IConfiguration configuration) : IRagClient
{
    private string BaseUrl => configuration["RagCore:BaseUrl"] ?? "http://localhost:8000";
    private string InternalSecret => GetRequiredInternalSecret(configuration);

    public async Task<RagQueryResult> QueryAsync(string tenantSlug, string query)
    {
        var body = JsonSerializer.Serialize(new { tenant_id = tenantSlug, query });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/internal/query")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Internal-Secret", InternalSecret);

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var answer = root.GetProperty("answer").GetString() ?? string.Empty;
        var sources = new List<RagSource>();
        if (root.TryGetProperty("sources", out var sourcesEl))
        {
            foreach (var s in sourcesEl.EnumerateArray())
            {
                sources.Add(new RagSource(
                    File: s.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "",
                    Page: s.TryGetProperty("page", out var p) && p.ValueKind != JsonValueKind.Null ? p.GetInt32() : null,
                    Offset: s.TryGetProperty("offset", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetInt32() : null,
                    RelevanceScore: s.TryGetProperty("relevance_score", out var r) && r.ValueKind != JsonValueKind.Null ? (float?)r.GetDouble() : null,
                    BriefContent: s.TryGetProperty("brief_content", out var bc) ? bc.GetString() ?? "" : ""
                ));
            }
        }

        return new RagQueryResult(answer, sources);
    }

    public async Task<RagIngestResult> IngestAsync(string tenantSlug, string documentId, string fileName, byte[] fileBytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(tenantSlug), "tenant_id");
        form.Add(new StringContent(documentId), "document_id");

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/internal/ingest")
        {
            Content = form,
        };
        request.Headers.Add("X-Internal-Secret", InternalSecret);

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var chunksWritten = root.TryGetProperty("chunks_written", out var cw) ? cw.GetInt32() : 0;
        var returnedDocId = root.TryGetProperty("document_id", out var did) ? did.GetString() ?? documentId : documentId;

        return new RagIngestResult(chunksWritten, returnedDocId);
    }

    private static string GetRequiredInternalSecret(IConfiguration cfg)
    {
        var secret = cfg["RagCore:InternalSecret"];
        if (!string.IsNullOrWhiteSpace(secret))
            return secret;

        throw new InvalidOperationException(
            "RagCore:InternalSecret is not configured. Use RagCore__InternalSecret or RagCore__InternalSecret_FILE.");
    }
}
