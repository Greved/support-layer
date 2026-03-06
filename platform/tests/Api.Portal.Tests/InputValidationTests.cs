using System.Net;
using System.Text;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Data;

namespace Api.Portal.Tests;

[TestFixture]
public class InputValidationTests
{
    private PortalApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PortalApiFactory();
        await _factory.InitAsync();
        _client = _factory.CreateClient();

        var db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await SeedHelper.SeedTenantAsync(db, "validation-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "val@test.com");
        _client.SetPortalToken(user.Id, tenant.Id);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task Chat_EmptyQuery_Returns422()
    {
        var payload = new StringContent(
            JsonSerializer.Serialize(new { query = "" }),
            Encoding.UTF8,
            "application/json");

        var resp = await _client.PostAsync("/portal/chat", payload);

        // Empty string fails model validation (required field)
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Chat_QueryOver10000Chars_Returns400()
    {
        var longQuery = new string('a', 10_001);
        var payload = new StringContent(
            JsonSerializer.Serialize(new { query = longQuery }),
            Encoding.UTF8,
            "application/json");

        var resp = await _client.PostAsync("/portal/chat", payload);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("query_too_long");
    }

    [Test]
    public async Task Chat_ExactlyMaxLengthQuery_IsNotRejectedByLengthGuard()
    {
        // 10 000 chars is the boundary — should NOT be rejected by the length guard
        // (It may fail for other reasons like RAG not running, but not 400 query_too_long)
        var maxQuery = new string('a', 10_000);
        var payload = new StringContent(
            JsonSerializer.Serialize(new { query = maxQuery }),
            Encoding.UTF8,
            "application/json");

        var resp = await _client.PostAsync("/portal/chat", payload);

        resp.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "10 000 chars is within limit and should not trigger query_too_long");
    }
}
