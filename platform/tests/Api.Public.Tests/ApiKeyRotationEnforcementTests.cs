using System.Net;
using System.Text;
using System.Text.Json;
using Api.Public.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Public.Tests;

[TestFixture]
public class ApiKeyRotationEnforcementTests
{
    private PublicApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PublicApiFactory();
        await _factory.InitAsync();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private static StringContent ChatPayload(string query) =>
        new(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");

    [Test]
    public async Task RevokedKey_IsRejected_AndNewKeyWorksImmediately()
    {
        Guid tenantId;
        Guid oldKeyId;
        string oldPlaintext;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = await SeedHelper.EnsureTenantAsync(db, "rotation-phase5-corp");
            tenantId = tenant.Id;

            var seeded = await SeedHelper.CreateApiKeyForTenantAsync(db, tenantId, "old-key");
            oldKeyId = seeded.key.Id;
            oldPlaintext = seeded.plaintext;
        }

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", oldPlaintext);

        var beforeRevoke = await _client.PostAsync("/v1/chat", ChatPayload("confirm key is active"));
        beforeRevoke.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oldKey = await db.ApiKeys.FindAsync(oldKeyId);
            oldKey.Should().NotBeNull();
            oldKey!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var afterRevoke = await _client.PostAsync("/v1/chat", ChatPayload("old key should fail now"));
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string newPlaintext;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var created = await SeedHelper.CreateApiKeyForTenantAsync(db, tenantId, "new-key");
            newPlaintext = created.plaintext;
        }

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", newPlaintext);

        var withNewKey = await _client.PostAsync("/v1/chat", ChatPayload("new key should work"));
        withNewKey.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ExpiredKey_IsRejectedWith401()
    {
        string expiredPlaintext;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = await SeedHelper.EnsureTenantAsync(db, "expired-phase5-corp");
            var created = await SeedHelper.CreateApiKeyForTenantAsync(
                db,
                tenant.Id,
                "expired-key",
                expiresAt: DateTime.UtcNow.AddMinutes(-1));
            expiredPlaintext = created.plaintext;
        }

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", expiredPlaintext);

        var resp = await _client.PostAsync("/v1/chat", ChatPayload("expired key check"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("expired");
    }
}
