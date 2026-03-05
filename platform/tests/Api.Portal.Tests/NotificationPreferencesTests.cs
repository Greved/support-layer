using System.Net;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class NotificationPreferencesTests
{
    private PortalApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PortalApiFactory();
        await _factory.InitAsync();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private AppDbContext Db()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [Test]
    public async Task GetNotifications_Returns200WithDefaultToggles()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "notif-get-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@notifget.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var resp = await _client.GetAsync("/portal/settings/notifications");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        var prefs = body.GetProperty("preferences").EnumerateArray().ToList();
        prefs.Count.Should().Be(4);
        prefs.All(p => p.GetProperty("emailEnabled").GetBoolean()).Should().BeTrue();
        prefs.All(p => p.GetProperty("inAppEnabled").GetBoolean()).Should().BeTrue();
    }

    [Test]
    public async Task UpdateNotifications_PersistsChanges()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "notif-upd-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@notifupd.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var payload = new
        {
            Preferences = new[]
            {
                new { EventType = "ingestion.complete", EmailEnabled = false, InAppEnabled = true },
                new { EventType = "quota.80", EmailEnabled = true, InAppEnabled = false },
            }
        };

        var resp = await _client.PutAsync("/portal/settings/notifications", HttpHelper.Json(payload));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Read back
        var getResp = await _client.GetAsync("/portal/settings/notifications");
        var body = await getResp.ReadJson<JsonElement>();
        var prefs = body.GetProperty("preferences").EnumerateArray().ToList();

        var ingestionPref = prefs.First(p => p.GetProperty("eventType").GetString() == "ingestion.complete");
        ingestionPref.GetProperty("emailEnabled").GetBoolean().Should().BeFalse();
        ingestionPref.GetProperty("inAppEnabled").GetBoolean().Should().BeTrue();

        var quotaPref = prefs.First(p => p.GetProperty("eventType").GetString() == "quota.80");
        quotaPref.GetProperty("emailEnabled").GetBoolean().Should().BeTrue();
        quotaPref.GetProperty("inAppEnabled").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task GetNotifications_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/portal/settings/notifications");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateNotifications_TenantIsolation()
    {
        var db = Db();
        var tenantA = await SeedHelper.SeedTenantAsync(db, "notif-iso-a");
        var tenantB = await SeedHelper.SeedTenantAsync(db, "notif-iso-b");
        var userA = await SeedHelper.SeedUserAsync(db, tenantA, "a@notifiso.com");
        var userB = await SeedHelper.SeedUserAsync(db, tenantB, "b@notifiso.com");

        // Tenant A disables email for ingestion.complete
        _client.SetPortalToken(userA.Id, tenantA.Id);
        await _client.PutAsync("/portal/settings/notifications", HttpHelper.Json(new
        {
            Preferences = new[] { new { EventType = "ingestion.complete", EmailEnabled = false, InAppEnabled = true } }
        }));

        // Tenant B should still see defaults (emailEnabled=true)
        _client.SetPortalToken(userB.Id, tenantB.Id);
        var resp = await _client.GetAsync("/portal/settings/notifications");
        var body = await resp.ReadJson<JsonElement>();
        var pref = body.GetProperty("preferences").EnumerateArray()
            .First(p => p.GetProperty("eventType").GetString() == "ingestion.complete");
        pref.GetProperty("emailEnabled").GetBoolean().Should().BeTrue();
    }
}
