using System.Net;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class DashboardTests
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
    public async Task GetUsage_Returns200WithExpectedShape()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "dash-usage-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@dashusage.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var resp = await _client.GetAsync("/portal/dashboard/usage");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("queriesThisMonth").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("documentCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("teamMemberCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("planLimits").GetProperty("maxDocuments").GetInt32().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetUsage_CountsMatchSeededData()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "dash-count-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@dashcount.com");

        // Seed 3 billing events with "query" type in current month
        for (int i = 0; i < 3; i++)
            await SeedHelper.SeedBillingEventAsync(db, tenant, "query");

        _client.SetPortalToken(user.Id, tenant.Id);
        var resp = await _client.GetAsync("/portal/dashboard/usage");

        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("queriesThisMonth").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        body.GetProperty("teamMemberCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetUsage_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/portal/dashboard/usage");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUsage_TenantIsolation_CountsOnlyOwnData()
    {
        var db = Db();
        var tenantA = await SeedHelper.SeedTenantAsync(db, "dash-iso-a");
        var tenantB = await SeedHelper.SeedTenantAsync(db, "dash-iso-b");
        var userA = await SeedHelper.SeedUserAsync(db, tenantA, "a@dashiso.com");
        var userB = await SeedHelper.SeedUserAsync(db, tenantB, "b@dashiso.com");

        // Seed 5 events for tenant A
        for (int i = 0; i < 5; i++)
            await SeedHelper.SeedBillingEventAsync(db, tenantA, "query");

        // Tenant B should see 0 queries
        _client.SetPortalToken(userB.Id, tenantB.Id);
        var resp = await _client.GetAsync("/portal/dashboard/usage");
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("queriesThisMonth").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task GetUsage_MemberRoleEnforcement_Returns200()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "dash-role-corp");
        var owner = await SeedHelper.SeedUserAsync(db, tenant, "owner@dashrole.com", "owner");
        var member = await SeedHelper.SeedUserAsync(db, tenant, "member@dashrole.com", "member");

        // Member can read usage dashboard (read-only endpoint)
        _client.SetPortalToken(member.Id, tenant.Id, "member");
        var resp = await _client.GetAsync("/portal/dashboard/usage");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
