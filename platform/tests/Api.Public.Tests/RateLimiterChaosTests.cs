using System.Net;
using System.Text;
using System.Text.Json;
using Api.Public.Services;
using Api.Public.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Public.Tests;

[TestFixture]
public class RateLimiterChaosTests
{
    private PublicApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PublicApiFactory();
        await _factory.InitAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task Chat_WhenRedisRateLimiterUnavailable_FailsOpenAndReturns200()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRateLimiter>();
                services.AddSingleton<IRateLimiter>(new UnavailableRedisRateLimiter());
            });
        }).CreateClient();

        var db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, plaintext) = await SeedHelper.SeedTenantWithApiKeyAsync(db, "chat-redis-down-corp");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", plaintext);

        var payload = new StringContent(
            JsonSerializer.Serialize(new { query = "Are you available during redis outage?" }),
            Encoding.UTF8,
            "application/json");

        var resp = await client.PostAsync("/v1/chat", payload);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("answer");
    }
}
