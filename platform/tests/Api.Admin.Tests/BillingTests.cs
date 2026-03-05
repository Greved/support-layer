using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

[TestFixture]
public class BillingTests
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
    public async Task GetBilling_TenantWithEvents_Returns200WithData()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "billing-corp");
        await SeedHelper.SeedBillingEventAsync(db, tenant);
        await SeedHelper.SeedBillingEventAsync(db, tenant);

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/billing");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("tenantId").GetString().Should().Be(tenant.Id.ToString());
        body.GetProperty("eventCount30d").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        body.GetProperty("recentEvents").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task GetBilling_TenantNoEvents_Returns200WithZeroCounts()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "billing-empty-corp");

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/billing");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("eventCount30d").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task GetBilling_UnknownTenant_Returns404()
    {
        Auth();
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/billing");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetBilling_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/billing");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
