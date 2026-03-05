using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

[TestFixture]
public class TenantsTests
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

    // ── List ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetTenants_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/admin/tenants");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetTenants_Authenticated_Returns200WithItems()
    {
        Auth();
        await SeedHelper.SeedTenantAsync(Db(), "list-tenant-a");

        var resp = await _client.GetAsync("/admin/tenants");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetTenants_SearchFilter_ReturnsMatchingTenant()
    {
        Auth();
        await SeedHelper.SeedTenantAsync(Db(), "searchable-corp");

        var resp = await _client.GetAsync("/admin/tenants?search=searchable-corp");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        var items = body.GetProperty("items");
        items.EnumerateArray().Should().Contain(t =>
            t.GetProperty("slug").GetString() == "searchable-corp");
    }

    [Test]
    public async Task GetTenants_InactiveFilter_ReturnsOnlyInactive()
    {
        Auth();
        var db = Db();
        var t = await SeedHelper.SeedTenantAsync(db, "inactive-tenant");
        t.IsActive = false;
        await db.SaveChangesAsync();

        var resp = await _client.GetAsync("/admin/tenants?isActive=false");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        foreach (var item in body.GetProperty("items").EnumerateArray())
            item.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    [Test]
    public async Task GetTenant_ExistingId_Returns200WithDetail()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "detail-corp");

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(tenant.Id.ToString());
        body.GetProperty("slug").GetString().Should().Be("detail-corp");
    }

    [Test]
    public async Task GetTenant_UnknownId_Returns404()
    {
        Auth();
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateTenant_ValidRequest_Returns201()
    {
        Auth();
        var resp = await _client.PostAsync("/admin/tenants",
            HttpHelper.Json(new { Name = "Beta Corp", Slug = "beta-corp", PlanSlug = "free" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("slug").GetString().Should().Be("beta-corp");
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task CreateTenant_DuplicateSlug_Returns409()
    {
        Auth();
        await SeedHelper.SeedTenantAsync(Db(), "dupe-slug");

        var resp = await _client.PostAsync("/admin/tenants",
            HttpHelper.Json(new { Name = "Dupe", Slug = "dupe-slug", PlanSlug = "free" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateTenant_UnknownPlan_Returns400()
    {
        Auth();
        var resp = await _client.PostAsync("/admin/tenants",
            HttpHelper.Json(new { Name = "X", Slug = "x-corp-badplan", PlanSlug = "nonexistent" }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateTenant_ChangePlan_Returns200WithNewPlan()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "plan-change-corp");

        var resp = await _client.PatchAsync($"/admin/tenants/{tenant.Id}",
            HttpHelper.Json(new { PlanSlug = "pro" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("plan").GetString().Should().Be("pro");
    }

    [Test]
    public async Task UpdateTenant_Suspend_SetsIsActiveFalse()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "suspend-corp");

        var resp = await _client.PatchAsync($"/admin/tenants/{tenant.Id}",
            HttpHelper.Json(new { IsActive = false }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteTenant_SoftDeletes_Returns204()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "delete-corp");

        var resp = await _client.DeleteAsync($"/admin/tenants/{tenant.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var db = Db();
        var deleted = await db.Tenants.FindAsync(tenant.Id);
        deleted!.IsActive.Should().BeFalse();
    }

    [Test]
    public async Task DeleteTenant_Unknown_Returns404()
    {
        Auth();
        var resp = await _client.DeleteAsync($"/admin/tenants/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Impersonate ───────────────────────────────────────────────────────────

    [Test]
    public async Task Impersonate_TenantWithUser_Returns200WithPortalToken()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "impersonate-corp");
        await SeedHelper.SeedUserAsync(db, tenant, "owner@impersonate-corp.com");

        var resp = await _client.PostAsync($"/admin/tenants/{tenant.Id}/impersonate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("expiresInSeconds").GetInt32().Should().Be(900);
    }

    [Test]
    public async Task Impersonate_WritesAuditLog()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "audit-impersonate-corp");
        await SeedHelper.SeedUserAsync(db, tenant, "owner@audit-impersonate-corp.com");

        await _client.PostAsync($"/admin/tenants/{tenant.Id}/impersonate", null);

        var scope = _factory.Services.CreateScope();
        var verifyDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = verifyDb.AuditLogs.FirstOrDefault(a =>
            a.TenantId == tenant.Id && a.Action == "impersonate");
        log.Should().NotBeNull();
    }

    [Test]
    public async Task Impersonate_TenantWithNoUsers_Returns400()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "no-users-corp");

        var resp = await _client.PostAsync($"/admin/tenants/{tenant.Id}/impersonate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Export_ValidTenant_ReturnsZipFile()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "export-corp");

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/export");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }
}
