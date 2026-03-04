using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

public class BillingTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _adminId = Guid.NewGuid();

    private AppDbContext Db()
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private void Auth() => _client.SetAdminToken(_adminId);

    [Fact]
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

    [Fact]
    public async Task GetBilling_TenantNoEvents_Returns200WithZeroCounts()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "billing-empty-corp");

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/billing");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("eventCount30d").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetBilling_UnknownTenant_Returns404()
    {
        Auth();
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/billing");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBilling_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/billing");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
