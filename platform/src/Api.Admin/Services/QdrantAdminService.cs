using System.Text.Json;
using Api.Admin.Models.Responses;

namespace Api.Admin.Services;

public class QdrantAdminService(IConfiguration configuration, IHttpClientFactory httpFactory) : IQdrantAdminService
{
    private string BaseUrl => configuration["Qdrant:BaseUrl"] ?? "http://localhost:6333";

    public async Task<IReadOnlyList<CollectionInfoResponse>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient();
        var response = await client.GetAsync($"{BaseUrl}/collections", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var result = new List<CollectionInfoResponse>();
        if (!doc.RootElement.TryGetProperty("result", out var resultEl) ||
            !resultEl.TryGetProperty("collections", out var collections))
            return result;

        foreach (var col in collections.EnumerateArray())
        {
            var name = col.GetProperty("name").GetString() ?? string.Empty;
            // Fetch info for each collection
            try
            {
                var infoResp = await client.GetAsync($"{BaseUrl}/collections/{name}", ct);
                if (!infoResp.IsSuccessStatusCode) continue;
                var infoJson = await infoResp.Content.ReadAsStringAsync(ct);
                using var infoDoc = JsonDocument.Parse(infoJson);
                var info = infoDoc.RootElement.GetProperty("result");
                var vectors = info.TryGetProperty("vectors_count", out var vc) ? vc.GetInt64() : 0;
                var points = info.TryGetProperty("points_count", out var pc) ? pc.GetInt64() : 0;
                var slug = name.StartsWith("tenant_") ? name["tenant_".Length..] : name;
                result.Add(new CollectionInfoResponse(name, slug, vectors, points));
            }
            catch
            {
                // skip collections that can't be queried
            }
        }

        return result;
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient();
        var response = await client.DeleteAsync($"{BaseUrl}/collections/{collectionName}", ct);
        response.EnsureSuccessStatusCode();
    }
}
