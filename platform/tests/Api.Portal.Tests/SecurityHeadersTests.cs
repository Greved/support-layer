using System.Net;
using Api.Portal.Tests.Helpers;
using FluentAssertions;

namespace Api.Portal.Tests;

[TestFixture]
public class SecurityHeadersTests
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

    [Test]
    public async Task SecurityHeaders_ArePresent_OnHealthzResponse()
    {
        var resp = await _client.GetAsync("/healthz");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        resp.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
    }

    [Test]
    public async Task SecurityHeaders_XssProtection_IsDisabled()
    {
        var resp = await _client.GetAsync("/healthz");

        resp.Headers.GetValues("X-XSS-Protection").Should().Contain("0");
    }

    [Test]
    public async Task SecurityHeaders_ReferrerPolicy_IsPresent()
    {
        var resp = await _client.GetAsync("/healthz");

        resp.Headers.GetValues("Referrer-Policy").Should().Contain("strict-origin-when-cross-origin");
    }

    [Test]
    public async Task SecurityHeaders_ContentSecurityPolicy_IsPresent()
    {
        var resp = await _client.GetAsync("/healthz");

        resp.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }
}
