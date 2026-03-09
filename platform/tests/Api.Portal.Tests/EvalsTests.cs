using System.Net;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class EvalsTests
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
    public async Task EvalsSummary_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/portal/evals/summary");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task EvalsSummary_ReturnsCurrentAndPrevious()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"portal-evals-{Guid.NewGuid():N}");
        var user = await SeedHelper.SeedUserAsync(db, tenant, $"owner-{Guid.NewGuid():N}@portal-evals.com");

        var dataset = await SeedHelper.SeedEvalDatasetAsync(
            db,
            tenant,
            question: "Q1",
            groundTruth: "A1");

        var prevRun = await SeedHelper.SeedEvalRunAsync(db, tenant, runType: "manual");
        await SeedHelper.SeedEvalResultAsync(db, prevRun, dataset, faithfulness: 0.80);

        var currRun = await SeedHelper.SeedEvalRunAsync(db, tenant, runType: "manual");
        await SeedHelper.SeedEvalResultAsync(db, currRun, dataset, faithfulness: 0.92);

        _client.SetPortalToken(user.Id, tenant.Id);
        var resp = await _client.GetAsync("/portal/evals/summary");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("currentRunId").GetGuid().Should().Be(currRun.Id);
        body.GetProperty("currentScores").GetProperty("faithfulness").GetDouble().Should().Be(0.92);
        body.GetProperty("previousScores").GetProperty("faithfulness").GetDouble().Should().Be(0.80);
    }

    [Test]
    public async Task EvalsRunsAndDetail_AreTenantScoped()
    {
        var db = Db();
        var tenantA = await SeedHelper.SeedTenantAsync(db, $"portal-evals-a-{Guid.NewGuid():N}");
        var userA = await SeedHelper.SeedUserAsync(db, tenantA, $"owner-a-{Guid.NewGuid():N}@portal-evals.com");
        var datasetA = await SeedHelper.SeedEvalDatasetAsync(db, tenantA, question: "Tenant A question", groundTruth: "Tenant A answer");
        var runA = await SeedHelper.SeedEvalRunAsync(db, tenantA);
        await SeedHelper.SeedEvalResultAsync(db, runA, datasetA);

        var tenantB = await SeedHelper.SeedTenantAsync(db, $"portal-evals-b-{Guid.NewGuid():N}");
        var userB = await SeedHelper.SeedUserAsync(db, tenantB, $"owner-b-{Guid.NewGuid():N}@portal-evals.com");
        var datasetB = await SeedHelper.SeedEvalDatasetAsync(db, tenantB, question: "Tenant B question", groundTruth: "Tenant B answer");
        var runB = await SeedHelper.SeedEvalRunAsync(db, tenantB);
        await SeedHelper.SeedEvalResultAsync(db, runB, datasetB);

        _client.SetPortalToken(userA.Id, tenantA.Id);
        var listResp = await _client.GetAsync("/portal/evals/runs");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResp.ReadJson<JsonElement>();
        var items = listBody.GetProperty("items");
        items.EnumerateArray().Should().Contain(x => x.GetProperty("runId").GetGuid() == runA.Id);
        items.EnumerateArray().Should().NotContain(x => x.GetProperty("runId").GetGuid() == runB.Id);

        var detailResp = await _client.GetAsync($"/portal/evals/runs/{runA.Id}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detailResp.ReadJson<JsonElement>();
        detailBody.GetProperty("run").GetProperty("runId").GetGuid().Should().Be(runA.Id);
        detailBody.GetProperty("results").EnumerateArray().Should().Contain(x =>
            x.GetProperty("question").GetString() == "Tenant A question");

        _client.SetPortalToken(userB.Id, tenantB.Id);
        var foreignResp = await _client.GetAsync($"/portal/evals/runs/{runA.Id}");
        foreignResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task EvalsRun_ManualTrigger_CreatesCompletedRunWithResults()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"portal-eval-run-{Guid.NewGuid():N}");
        var user = await SeedHelper.SeedUserAsync(db, tenant, $"owner-run-{Guid.NewGuid():N}@portal-evals.com");
        await SeedHelper.SeedEvalDatasetAsync(db, tenant, question: "Q-run-1", groundTruth: "A-run-1");
        await SeedHelper.SeedEvalDatasetAsync(db, tenant, question: "Q-run-2", groundTruth: "A-run-2", questionType: "feedback_negative");

        _client.SetPortalToken(user.Id, tenant.Id);
        var triggerResp = await _client.PostAsync(
            "/portal/evals/run",
            HttpHelper.Json(new { runType = "manual", triggeredBy = "portal-test" }));

        triggerResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var triggerBody = await triggerResp.ReadJson<JsonElement>();
        var runId = triggerBody.GetProperty("runId").GetGuid();
        runId.Should().NotBe(Guid.Empty);
        triggerBody.GetProperty("status").GetString().Should().Be("completed");

        var listResp = await _client.GetAsync("/portal/evals/runs");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResp.ReadJson<JsonElement>();
        listBody.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("runId").GetGuid() == runId &&
            x.GetProperty("resultCount").GetInt32() == 2);

        var detailResp = await _client.GetAsync($"/portal/evals/runs/{runId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detailResp.ReadJson<JsonElement>();
        detailBody.GetProperty("run").GetProperty("runId").GetGuid().Should().Be(runId);
        var configSnapshotJson = detailBody.GetProperty("run").GetProperty("configSnapshotJson").GetString();
        configSnapshotJson.Should().NotBeNullOrWhiteSpace();
        using (var runSnapshotFromApiDoc = JsonDocument.Parse(configSnapshotJson!))
        {
            runSnapshotFromApiDoc.RootElement.GetProperty("schema").GetString()
                .Should().Be("phase6.eval-run-context.v1");
        }

        var firstResultFromApi = detailBody.GetProperty("results").EnumerateArray().First();
        var retrievedChunksJson = firstResultFromApi.GetProperty("retrievedChunksJson").GetString();
        retrievedChunksJson.Should().NotBeNullOrWhiteSpace();
        using (var retrievedChunksFromApiDoc = JsonDocument.Parse(retrievedChunksJson!))
        {
            Assert.That(retrievedChunksFromApiDoc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        }

        var resultSnapshotJson = firstResultFromApi.GetProperty("contextSnapshotJson").GetString();
        resultSnapshotJson.Should().NotBeNullOrWhiteSpace();
        using (var resultSnapshotFromApiDoc = JsonDocument.Parse(resultSnapshotJson!))
        {
            resultSnapshotFromApiDoc.RootElement.GetProperty("schema").GetString()
                .Should().Be("phase6.eval-result-context.v1");
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedRun = await verifyDb.EvalRuns.SingleAsync(r => r.Id == runId);
        using var runSnapshotDoc = JsonDocument.Parse(persistedRun.ConfigSnapshotJson);
        Assert.That(
            runSnapshotDoc.RootElement.GetProperty("schema").GetString(),
            Is.EqualTo("phase6.eval-run-context.v1"));

        var persistedResult = await verifyDb.EvalResults
            .Where(r => r.RunId == runId)
            .OrderBy(r => r.CreatedAt)
            .FirstAsync();
        using var resultSnapshotDoc = JsonDocument.Parse(persistedResult.ContextSnapshotJson);
        Assert.That(
            resultSnapshotDoc.RootElement.GetProperty("schema").GetString(),
            Is.EqualTo("phase6.eval-result-context.v1"));
        using var retrievedChunksDoc = JsonDocument.Parse(persistedResult.RetrievedChunksJson);
        Assert.That(retrievedChunksDoc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    public async Task EvalsRun_WhenRunAlreadyRunning_Returns409WithExistingRunId()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"portal-eval-running-{Guid.NewGuid():N}");
        var user = await SeedHelper.SeedUserAsync(db, tenant, $"owner-running-{Guid.NewGuid():N}@portal-evals.com");
        var runningRun = await SeedHelper.SeedEvalRunAsync(db, tenant, status: "running");

        _client.SetPortalToken(user.Id, tenant.Id);
        var resp = await _client.PostAsync(
            "/portal/evals/run",
            HttpHelper.Json(new { runType = "manual", triggeredBy = "portal-test" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("runId").GetGuid().Should().Be(runningRun.Id);
        body.GetProperty("error").GetString().Should().Be("eval_run_already_in_progress");
    }
}
