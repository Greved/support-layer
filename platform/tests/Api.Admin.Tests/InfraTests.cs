using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using FluentAssertions;

namespace Api.Admin.Tests;

[TestFixture]
public class InfraTests
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

    private void Auth() => _client.SetAdminToken(_adminId);

    [Test]
    public async Task GetHealth_ReturnsOverallStatusAndServices()
    {
        Auth();
        var resp = await _client.GetAsync("/admin/infra/health");

        // 200 = healthy, 207 = partially degraded — either is valid
        ((int)resp.StatusCode).Should().BeOneOf(200, 207);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("overall").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("services").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetHealth_ServicesHaveNameAndStatus()
    {
        Auth();
        var resp = await _client.GetAsync("/admin/infra/health");
        var body = await resp.ReadJson<JsonElement>();

        foreach (var svc in body.GetProperty("services").EnumerateArray())
        {
            svc.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
            svc.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Test]
    public async Task GetCollections_ReturnsList()
    {
        Auth();
        var resp = await _client.GetAsync("/admin/infra/collections");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GetHealth_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/admin/infra/health");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Healthz_NoAuth_Returns200()
    {
        var resp = await _client.GetAsync("/healthz");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("ok");
    }
}
