using System.Net;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class OnboardingTests
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
    public async Task GetOnboarding_NewTenant_ReturnsEmptyCompletedSteps()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "onboard-new-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@onboardnew.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var resp = await _client.GetAsync("/portal/onboarding");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("completedSteps").GetArrayLength().Should().Be(0);
        body.GetProperty("isComplete").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task CompleteStep_MarksStepDone()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "onboard-step-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@onboardstep.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var resp = await _client.PostAsync("/portal/onboarding/complete/1", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("completedSteps").EnumerateArray().Select(s => s.GetInt32())
            .Should().Contain(1);
    }

    [Test]
    public async Task CompleteStep_Idempotent_DoesNotDuplicate()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "onboard-idem-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@onboardidem.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        await _client.PostAsync("/portal/onboarding/complete/2", null);
        await _client.PostAsync("/portal/onboarding/complete/2", null);

        var resp = await _client.GetAsync("/portal/onboarding");
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("completedSteps").EnumerateArray()
            .Where(s => s.GetInt32() == 2).Should().HaveCount(1);
    }

    [Test]
    public async Task CompleteAllSteps_SetsIsCompleteTrue()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "onboard-all-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@onboardall.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        for (int step = 1; step <= 4; step++)
            await _client.PostAsync($"/portal/onboarding/complete/{step}", null);

        var resp = await _client.GetAsync("/portal/onboarding");
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("isComplete").GetBoolean().Should().BeTrue();
        body.GetProperty("completedSteps").GetArrayLength().Should().Be(4);
    }

    [Test]
    public async Task CompleteStep_InvalidStep_Returns400()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "onboard-bad-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "owner@onboardbad.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var resp = await _client.PostAsync("/portal/onboarding/complete/99", null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Onboarding_TenantIsolation()
    {
        var db = Db();
        var tenantA = await SeedHelper.SeedTenantAsync(db, "onboard-iso-a");
        var tenantB = await SeedHelper.SeedTenantAsync(db, "onboard-iso-b");
        var userA = await SeedHelper.SeedUserAsync(db, tenantA, "a@onboardiso.com");
        var userB = await SeedHelper.SeedUserAsync(db, tenantB, "b@onboardiso.com");

        _client.SetPortalToken(userA.Id, tenantA.Id);
        await _client.PostAsync("/portal/onboarding/complete/1", null);
        await _client.PostAsync("/portal/onboarding/complete/2", null);

        _client.SetPortalToken(userB.Id, tenantB.Id);
        var resp = await _client.GetAsync("/portal/onboarding");
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("completedSteps").GetArrayLength().Should().Be(0);
    }
}
