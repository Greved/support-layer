using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

[TestFixture]
public class DocumentsTests
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
    public async Task ListDocuments_ReturnsTenantDocuments()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "docs-corp");
        await SeedHelper.SeedDocumentAsync(db, tenant);

        var resp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/documents");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetArrayLength().Should().BeGreaterThan(0);
        body[0].GetProperty("fileName").GetString().Should().Be("test.pdf");
    }

    [Test]
    public async Task ListDocuments_UnknownTenant_Returns404()
    {
        Auth();
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/documents");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteDocument_SoftDeletes_Returns204()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "docs-delete-corp");
        var doc = await SeedHelper.SeedDocumentAsync(db, tenant);

        var resp = await _client.DeleteAsync($"/admin/tenants/{tenant.Id}/documents/{doc.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var verifyDb = _factory.Services.CreateScope()
            .ServiceProvider.GetRequiredService<AppDbContext>();
        var deleted = await verifyDb.Documents.FindAsync(doc.Id);
        deleted!.IsActive.Should().BeFalse();
    }

    [Test]
    public async Task DeleteDocument_WrongTenant_Returns404()
    {
        Auth();
        var db = Db();
        var tenantA = await SeedHelper.SeedTenantAsync(db, "docs-tenant-a");
        var tenantB = await SeedHelper.SeedTenantAsync(db, "docs-tenant-b");
        var doc = await SeedHelper.SeedDocumentAsync(db, tenantA);

        // Try to delete tenantA's doc via tenantB's route
        var resp = await _client.DeleteAsync($"/admin/tenants/{tenantB.Id}/documents/{doc.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListDocuments_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/documents");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
