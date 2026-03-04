using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Api.Admin.Tests.Helpers;

public static class HttpHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static void SetAdminToken(this HttpClient client, Guid adminId)
        => client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.AdminToken(adminId));

    public static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    public static async Task<T?> ReadJson<T>(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }
}
