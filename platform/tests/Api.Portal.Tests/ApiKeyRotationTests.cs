using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Data;

namespace Api.Portal.Tests;

[TestFixture]
public class ApiKeyRotationTests
{
    private PortalApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _userId;
    private Guid _tenantId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PortalApiFactory();
        await _factory.InitAsync();
        _client = _factory.CreateClient();

        var db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await SeedHelper.SeedTenantAsync(db, "apikey-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "apikey@test.com");
        _userId = user.Id;
        _tenantId = tenant.Id;
        _client.SetPortalToken(_userId, _tenantId);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task CreateApiKey_ReturnsPlaintextKey()
    {
        var payload = HttpHelper.Json(new { name = "Test Key" });

        var resp = await _client.PostAsync("/portal/api-keys", payload);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("plaintextKey").GetString().Should().StartWith("sl_live_");
        body.GetProperty("name").GetString().Should().Be("Test Key");
    }

    [Test]
    public async Task CreateApiKey_StoresLowercaseHash_ForPublicApiValidation()
    {
        var payload = HttpHelper.Json(new { name = "Hash Format Key" });

        var resp = await _client.PostAsync("/portal/api-keys", payload);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        var keyId = body.GetProperty("id").GetGuid();
        var plaintext = body.GetProperty("plaintextKey").GetString();
        plaintext.Should().NotBeNullOrWhiteSpace();

        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext!)));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ApiKeys.FindAsync(keyId);

        stored.Should().NotBeNull();
        stored!.KeyHash.Should().Be(expectedHash);
        stored.KeyHash.Should().Be(stored.KeyHash.ToLowerInvariant());
    }

    [Test]
    public async Task ListApiKeys_ShowsActiveKey()
    {
        // Create a key
        var createPayload = HttpHelper.Json(new { name = "List Test Key" });
        var createResp = await _client.PostAsync("/portal/api-keys", createPayload);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // List keys
        var listResp = await _client.GetAsync("/portal/api-keys");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var keys = await listResp.ReadJson<JsonElement[]>();
        keys.Should().NotBeNull();
        keys!.Any(k => k.GetProperty("name").GetString() == "List Test Key").Should().BeTrue();
    }

    [Test]
    public async Task RevokeApiKey_RemovesItFromListing()
    {
        // Create a key
        var createPayload = HttpHelper.Json(new { name = "Revoke Test Key" });
        var createResp = await _client.PostAsync("/portal/api-keys", createPayload);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResp.ReadJson<JsonElement>();
        var keyId = created.GetProperty("id").GetGuid().ToString();

        // Revoke it
        var deleteResp = await _client.DeleteAsync($"/portal/api-keys/{keyId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // List should not contain it anymore
        var listResp = await _client.GetAsync("/portal/api-keys");
        var keys = await listResp.ReadJson<JsonElement[]>();
        keys!.Any(k => k.GetProperty("id").GetGuid().ToString() == keyId).Should().BeFalse(
            "revoked key should not appear in active key listing");
    }

    [Test]
    public async Task RevokeNonExistentApiKey_Returns404()
    {
        var resp = await _client.DeleteAsync($"/portal/api-keys/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
