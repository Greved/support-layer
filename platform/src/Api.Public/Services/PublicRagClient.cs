using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Api.Public.Services;

public class PublicRagClient(HttpClient http, IConfiguration configuration) : IPublicRagClient
{
    private string BaseUrl => configuration["RagCore:BaseUrl"] ?? "http://localhost:8000";
    private string InternalSecret => GetRequiredInternalSecret(configuration);

    public async Task<PublicRagResult> QueryAsync(string tenantSlug, string query, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { tenant_id = tenantSlug, query });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/internal/query")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Internal-Secret", InternalSecret);

        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var answer = root.GetProperty("answer").GetString() ?? string.Empty;
        var sources = new List<PublicRagSource>();
        if (root.TryGetProperty("sources", out var sourcesEl))
        {
            foreach (var s in sourcesEl.EnumerateArray())
            {
                sources.Add(new PublicRagSource(
                    File: s.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "",
                    Page: s.TryGetProperty("page", out var p) && p.ValueKind != JsonValueKind.Null ? p.GetInt32() : null,
                    Offset: s.TryGetProperty("offset", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetInt32() : null,
                    RelevanceScore: s.TryGetProperty("relevance_score", out var r) && r.ValueKind != JsonValueKind.Null ? (float?)r.GetDouble() : null,
                    BriefContent: s.TryGetProperty("brief_content", out var bc) ? bc.GetString() ?? "" : ""
                ));
            }
        }

        return new PublicRagResult(answer, sources);
    }

    public async IAsyncEnumerable<string> StreamQueryAsync(
        string tenantSlug,
        string query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { tenant_id = tenantSlug, query });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/internal/stream")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Internal-Secret", InternalSecret);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (line.StartsWith("data:"))
                yield return line;
        }
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
