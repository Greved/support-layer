using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

[TestFixture]
public class StatsTests
{
    private AdminApiFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _adminId = Guid.NewGuid();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new AdminApiFactory();
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

    private void Auth() => _client.SetAdminToken(_adminId);

    [Test]
    public async Task TenantStats_ExistingTenant_Returns200WithCounts()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "stats-corp");
        await SeedHelper.SeedBillingEventAsync(db, tenant);
        await SeedHelper.SeedDocumentAsync(db, tenant);

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/stats");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("tenantId").GetString().Should().Be(tenant.Id.ToString());
        body.GetProperty("queriesLast30d").GetInt64().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("documentCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task TenantStats_UnknownTenant_Returns404()
    {
        Auth();
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GlobalStats_Returns200WithPlatformAggregates()
    {
        Auth();
        // Ensure at least one tenant exists
        await SeedHelper.SeedTenantAsync(Db(), "global-stats-corp");

        var resp = await _client.GetAsync("/admin/stats/global");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("totalTenants").GetInt32().Should().BeGreaterThan(0);
        body.TryGetProperty("queriesLast30d", out _).Should().BeTrue();
        body.TryGetProperty("totalDocuments", out _).Should().BeTrue();
    }

    [Test]
    public async Task TenantStats_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
