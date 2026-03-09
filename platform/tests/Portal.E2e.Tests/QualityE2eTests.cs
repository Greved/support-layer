using Microsoft.Playwright.NUnit;
using Portal.E2e.Tests.Pages;
using System.Text.Json;

namespace Portal.E2e.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class QualityE2eTests : PortalE2eTestBase
{
    [SetUp]
    public void RequireSpa()
    {
        Assume.That(GlobalSetup.SpaDistPath, Is.Not.Null,
            "Portal SPA dist/ not found. Run 'npm run build' in portal/ before E2E tests.");
    }

    private async Task LoginAsync()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.GoToAsync();
        await loginPage.LoginAsync(E2eSeeder.UserEmail, E2eSeeder.UserPassword);
        await Page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
    }

    private async Task<QualityPage> OpenQualityAsync()
    {
        var qualityPage = new QualityPage(Page);
        await qualityPage.GoToAsync();
        await qualityPage.WaitForLoadAsync();
        return qualityPage;
    }

    [Test]
    public async Task Quality_FromSidebar_RendersCoreWidgets()
    {
        await LoginAsync();

        await Expect(Page.Locator("text=Analysis")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        var qualityNav = Page.Locator("[data-testid='nav-quality'], a[href*='/quality']");
        await qualityNav.ClickAsync();
        await Page.WaitForURLAsync(u => u.Contains("/quality"), new() { Timeout = 10_000 });

        var qualityPage = new QualityPage(Page);
        await qualityPage.WaitForLoadAsync();

        await Expect(qualityPage.Root).ToBeVisibleAsync();
        await Expect(qualityPage.RunEvalButton).ToBeVisibleAsync();
        await Expect(qualityPage.RunHistoryChart).ToBeVisibleAsync();
        await Expect(qualityPage.LowConfidenceTable).ToBeVisibleAsync();
        await Expect(qualityPage.TraceView).ToBeVisibleAsync();

        Assert.That(Page.Url, Does.Contain("/quality"));
    }

    [Test]
    public async Task Quality_WhenSummaryApiFails_ShowsErrorBanner()
    {
        await LoginAsync();

        await Page.RouteAsync("**/portal/evals/summary", async route =>
            await route.FulfillAsync(new()
            {
                Status = 500,
                ContentType = "application/json",
                Body = "{\"error\":\"test-failure\"}",
            }));

        var qualityPage = await OpenQualityAsync();
        await Expect(qualityPage.ErrorBanner).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(qualityPage.ErrorBanner).ToContainTextAsync("Failed to load quality data.");
        Assert.That(Page.Url, Does.Contain("/quality"));
    }

    [Test]
    public async Task Quality_RunEvaluationAction_ShowsRecentRunsAndHistoryBars()
    {
        await LoginAsync();

        var qualityPage = await OpenQualityAsync();
        await qualityPage.TriggerEvalRunAsync();

        await Expect(qualityPage.Root).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(qualityPage.RunEvalButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(qualityPage.RunHistoryChart).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(qualityPage.RecentRunRows.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
        Assert.That(await qualityPage.RecentRunRows.CountAsync(), Is.GreaterThanOrEqualTo(1));
        Assert.That(await qualityPage.RunHistoryBars.CountAsync(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Quality_SelectRow_ShowsTraceAndFixInKnowledgeBaseNavigatesToDocuments()
    {
        await LoginAsync();

        var qualityPage = await OpenQualityAsync();
        await qualityPage.TriggerEvalRunAsync();
        await Expect(qualityPage.LowConfidenceRows.First).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await qualityPage.LowConfidenceRows.First.ClickAsync();
        await Expect(qualityPage.TraceSteps).ToHaveCountAsync(4, new() { Timeout = 10_000 });
        await Expect(qualityPage.TraceFixKnowledgeBaseButton).ToBeVisibleAsync();

        await qualityPage.TraceFixKnowledgeBaseButton.ClickAsync();
        await Page.WaitForURLAsync(u => u.Contains("/documents"), new() { Timeout = 15_000 });
        Assert.That(Page.Url, Does.Contain("/documents"));
        Assert.That(Page.Url, Does.Contain("focus="));
    }

    [Test]
    public async Task Quality_MarkCorrectFromRow_RemovesRowFromTable()
    {
        await LoginAsync();

        var qualityPage = await OpenQualityAsync();
        await qualityPage.TriggerEvalRunAsync();
        await Expect(qualityPage.LowConfidenceRows.First).ToBeVisibleAsync(new() { Timeout = 15_000 });

        var beforeCount = await qualityPage.LowConfidenceRows.CountAsync();
        Assert.That(beforeCount, Is.GreaterThan(0), "Expected at least one low-confidence row.");

        await qualityPage.RowMarkCorrectButtons.First.ClickAsync();

        await Expect(qualityPage.LowConfidenceRows).ToHaveCountAsync(beforeCount - 1, new() { Timeout = 15_000 });
        Assert.That(Page.Url, Does.Contain("/quality"));
    }

    [Test]
    public async Task Quality_RunEvaluation_RealEvalMode_UsesRagasAndDeepEvalWithoutFallback()
    {
        var realEvalSetting = Environment.GetEnvironmentVariable("E2E_REQUIRE_REAL_EVAL");
        if (string.Equals(realEvalSetting?.Trim(), "0", StringComparison.Ordinal))
            Assert.Ignore("Set E2E_REQUIRE_REAL_EVAL=1 or unset it to run real ragas/deepeval E2E assertion.");

        await LoginAsync();

        var qualityPage = await OpenQualityAsync();
        var runId = await qualityPage.TriggerEvalRunAsync();
        Assert.That(runId, Is.Not.Null.And.Not.Empty);

        var triggerContextJson = await qualityPage.FetchRunTriggerContextJsonAsync(runId!);
        Assert.That(triggerContextJson, Is.Not.Null.And.Not.Empty);

        using var triggerContext = JsonDocument.Parse(triggerContextJson!);
        var root = triggerContext.RootElement;

        Assert.That(root.GetProperty("usedFallback").GetBoolean(), Is.False);
        Assert.That(root.GetProperty("scoringEngine").GetString(), Is.EqualTo("python-eval"));

        var timings = root.GetProperty("timings");
        var rowsCount = timings.GetProperty("rows_count").GetDouble();
        Assert.That(timings.GetProperty("rows_with_ragas").GetDouble(), Is.EqualTo(rowsCount));
        Assert.That(timings.GetProperty("rows_with_deepeval").GetDouble(), Is.EqualTo(rowsCount));
        Assert.That(timings.GetProperty("rows_with_fallback").GetDouble(), Is.EqualTo(0));
    }
}
