using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

[TestFixture]
public class EvalsTests
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
    public async Task EvalsGlobal_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/admin/evals/global");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task TriggerRun_ListAndDetail_ReturnsCompletedRunWithResults()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"eval-run-{Guid.NewGuid():N}");
        await SeedHelper.SeedEvalDatasetAsync(
            db,
            tenant,
            question: "Where is billing page?",
            groundTruth: "Open Settings > Billing.",
            questionType: "synthetic");
        await SeedHelper.SeedEvalDatasetAsync(
            db,
            tenant,
            question: "Why answer was wrong?",
            groundTruth: "Because KB is outdated.",
            questionType: "feedback_negative");

        var triggerResp = await _client.PostAsync(
            $"/admin/tenants/{tenant.Id}/evals/run",
            HttpHelper.Json(new { runType = "manual", triggeredBy = "integration-test" }));

        triggerResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var triggerBody = await triggerResp.ReadJson<JsonElement>();
        var runId = triggerBody.GetProperty("runId").GetGuid();
        runId.Should().NotBe(Guid.Empty);
        triggerBody.GetProperty("status").GetString().Should().Be("completed");

        var listResp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/evals/runs");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResp.ReadJson<JsonElement>();
        listBody.ValueKind.Should().Be(JsonValueKind.Array);
        listBody.EnumerateArray().Should().Contain(r =>
            r.GetProperty("runId").GetGuid() == runId
            && r.GetProperty("resultCount").GetInt32() == 2
            && r.GetProperty("status").GetString() == "completed");

        var detailResp = await _client.GetAsync($"/admin/tenants/{tenant.Id}/evals/runs/{runId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detailResp.ReadJson<JsonElement>();
        detailBody.GetProperty("run").GetProperty("runId").GetGuid().Should().Be(runId);
        var results = detailBody.GetProperty("results");
        results.GetArrayLength().Should().Be(2);
        results.EnumerateArray().Should().Contain(x =>
            x.GetProperty("question").GetString() == "Where is billing page?");
    }

    [Test]
    public async Task GenerateDataset_Returns202_AndCreatesRows()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"eval-generate-{Guid.NewGuid():N}");
        await SeedHelper.SeedDocumentAsync(db, tenant);

        var resp = await _client.PostAsync($"/admin/tenants/{tenant.Id}/evals/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("tenantId").GetGuid().Should().Be(tenant.Id);
        body.GetProperty("generatedCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        body.GetProperty("status").GetString().Should().Be("completed");
        var version = body.GetProperty("datasetVersion").GetString();
        version.Should().NotBeNullOrWhiteSpace();

        var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = verifyDb.EvalDatasets
            .Where(d => d.TenantId == tenant.Id && d.DatasetVersion == version)
            .ToList();
        rows.Count.Should().BeGreaterThanOrEqualTo(3);
        rows.Any(r => r.QuestionType == "synthetic_simple").Should().BeTrue();
        rows.Any(r => r.QuestionType == "synthetic_multihop").Should().BeTrue();
        rows.Any(r => r.QuestionType == "synthetic_adversarial").Should().BeTrue();
    }

    [Test]
    public async Task EvalsGlobal_ReturnsLatestRunPerTenant()
    {
        Auth();
        var db = Db();

        var tenantA = await SeedHelper.SeedTenantAsync(db, $"eval-global-a-{Guid.NewGuid():N}");
        var datasetA = await SeedHelper.SeedEvalDatasetAsync(db, tenantA, question: "qa", groundTruth: "ga");
        var runA = await SeedHelper.SeedEvalRunAsync(db, tenantA);
        await SeedHelper.SeedEvalResultAsync(db, runA, datasetA, faithfulness: 0.91, relevancy: 0.90);

        var tenantB = await SeedHelper.SeedTenantAsync(db, $"eval-global-b-{Guid.NewGuid():N}");
        var datasetB = await SeedHelper.SeedEvalDatasetAsync(db, tenantB, question: "qb", groundTruth: "gb");
        var runB = await SeedHelper.SeedEvalRunAsync(db, tenantB);
        await SeedHelper.SeedEvalResultAsync(db, runB, datasetB, faithfulness: 0.82, relevancy: 0.80);

        var resp = await _client.GetAsync("/admin/evals/global");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.EnumerateArray().Should().Contain(r =>
            r.GetProperty("tenantId").GetGuid() == tenantA.Id
            && r.GetProperty("runId").GetGuid() == runA.Id);
        body.EnumerateArray().Should().Contain(r =>
            r.GetProperty("tenantId").GetGuid() == tenantB.Id
            && r.GetProperty("runId").GetGuid() == runB.Id);
    }

    [Test]
    public async Task BaselinePin_AndRegressionCheck_FlagsDegradedRun()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"eval-baseline-{Guid.NewGuid():N}");
        var dataset = await SeedHelper.SeedEvalDatasetAsync(db, tenant, question: "q", groundTruth: "g");

        var baselineRun = await SeedHelper.SeedEvalRunAsync(db, tenant);
        await SeedHelper.SeedEvalResultAsync(
            db,
            baselineRun,
            dataset,
            faithfulness: 0.90,
            relevancy: 0.90,
            precision: 0.90,
            recall: 0.90,
            hallucination: 0.05);

        var degradedRun = await SeedHelper.SeedEvalRunAsync(db, tenant);
        await SeedHelper.SeedEvalResultAsync(
            db,
            degradedRun,
            dataset,
            faithfulness: 0.50,
            relevancy: 0.80,
            precision: 0.80,
            recall: 0.80,
            hallucination: 0.10);

        var pinResp = await _client.PostAsync(
            "/admin/evals/baseline",
            HttpHelper.Json(new { runId = baselineRun.Id, setBy = "test-suite" }));

        pinResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var pinBody = await pinResp.ReadJson<JsonElement>();
        pinBody.GetProperty("runId").GetGuid().Should().Be(baselineRun.Id);

        var checkResp = await _client.PostAsync(
            "/admin/evals/regression-check",
            HttpHelper.Json(new { tenantId = tenant.Id, runId = degradedRun.Id }));

        checkResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkBody = await checkResp.ReadJson<JsonElement>();
        checkBody.GetProperty("failed").GetBoolean().Should().BeTrue();
        checkBody.GetProperty("metrics").EnumerateArray().Should().Contain(m =>
            m.GetProperty("metric").GetString() == "faithfulness"
            && m.GetProperty("failed").GetBoolean());
    }

    [Test]
    public async Task TriggerRun_WhenRunAlreadyRunning_Returns409WithExistingRunId()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"eval-running-{Guid.NewGuid():N}");
        var runningRun = await SeedHelper.SeedEvalRunAsync(db, tenant, status: "running");

        var resp = await _client.PostAsync(
            $"/admin/tenants/{tenant.Id}/evals/run",
            HttpHelper.Json(new { runType = "manual", triggeredBy = "integration-test" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("runId").GetGuid().Should().Be(runningRun.Id);
        body.GetProperty("error").GetString().Should().Be("eval_run_already_in_progress");
    }
}
