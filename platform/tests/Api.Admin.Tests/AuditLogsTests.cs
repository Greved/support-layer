using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using Core.Entities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

public class AuditLogsTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _adminId = Guid.NewGuid();

    private AppDbContext Db()
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private void Auth() => _client.SetAdminToken(_adminId);

    private async Task SeedAuditLogAsync(AppDbContext db, Tenant tenant, string action)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Action = action,
            ResourceType = "test",
            IpAddress = "127.0.0.1",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAuditLogs_Returns200WithPagedResult()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "audit-corp-list");
        await SeedAuditLogAsync(db, tenant, "CONFIG_UPDT");

        var resp = await _client.GetAsync("/admin/audit-logs");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("page").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetAuditLogs_FilterByTenantId_ReturnsOnlyThatTenant()
    {
        Auth();
        var db = Db();
        var tenantA = await SeedHelper.SeedTenantAsync(db, "audit-tenant-aa");
        var tenantB = await SeedHelper.SeedTenantAsync(db, "audit-tenant-bb");
        await SeedAuditLogAsync(db, tenantA, "EVT_A");
        await SeedAuditLogAsync(db, tenantB, "EVT_B");

        var resp = await _client.GetAsync($"/admin/audit-logs?tenantId={tenantA.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        foreach (var item in body.GetProperty("items").EnumerateArray())
            item.GetProperty("tenantId").GetString().Should().Be(tenantA.Id.ToString());
    }

    [Fact]
    public async Task GetAuditLogs_FilterByAction_ReturnsMatching()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "audit-action-corp");
        await SeedAuditLogAsync(db, tenant, "SPECIAL_ACTION");

        var resp = await _client.GetAsync("/admin/audit-logs?action=SPECIAL_ACTION");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        foreach (var item in body.GetProperty("items").EnumerateArray())
            item.GetProperty("action").GetString().Should().Contain("SPECIAL_ACTION");
    }

    [Fact]
    public async Task GetAuditLogs_FilterByDateRange_ReturnsOnlyInRange()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "audit-date-corp");
        await SeedAuditLogAsync(db, tenant, "DATE_TEST");

        var from = DateTime.UtcNow.AddMinutes(-1).ToString("O");
        var to = DateTime.UtcNow.AddMinutes(1).ToString("O");

        var resp = await _client.GetAsync($"/admin/audit-logs?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAuditLogs_Pagination_ReturnsCorrectPage()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "audit-page-corp");
        for (int i = 0; i < 5; i++)
            await SeedAuditLogAsync(db, tenant, $"PAGE_EVT_{i}");

        var resp = await _client.GetAsync($"/admin/audit-logs?tenantId={tenant.Id}&page=1&pageSize=2");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
        body.GetProperty("pageSize").GetInt32().Should().Be(2);
        body.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetAuditLogs_LogsHaveIpAddress()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "audit-ip-corp");
        await SeedAuditLogAsync(db, tenant, "IP_TEST");

        var resp = await _client.GetAsync($"/admin/audit-logs?tenantId={tenant.Id}&action=IP_TEST");

        var body = await resp.ReadJson<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("ipAddress").GetString().Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task GetAuditLogs_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/admin/audit-logs");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminAuditMiddleware_TenantMutation_WritesLog()
    {
        Auth();
        var tenant = await SeedHelper.SeedTenantAsync(Db(), "audit-mw-corp");

        // PATCH triggers the middleware
        await _client.PatchAsync($"/admin/tenants/{tenant.Id}",
            HttpHelper.Json(new { IsActive = true }));

        var db = Db();
        var log = db.AuditLogs.FirstOrDefault(a =>
            a.TenantId == tenant.Id && a.Action.Contains("PATCH"));
        log.Should().NotBeNull();
        log!.Action.Should().Contain("PATCH");
    }
}
