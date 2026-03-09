using System.Text.Json;
using Microsoft.Playwright;

namespace Portal.E2e.Tests.Pages;

/// <summary>Page object for the portal quality page.</summary>
public class QualityPage(IPage page)
{
    private static readonly int RunEvalTimeoutSeconds = ResolveRunEvalTimeoutSeconds();

    public ILocator Root => page.Locator("[data-testid='quality-page']");
    public ILocator ErrorBanner => page.Locator("[data-testid='quality-error-banner']");
    public ILocator RunEvalButton => page.Locator("[data-testid='quality-run-eval-btn']");
    public ILocator RunHistoryChart => page.Locator("[data-testid='quality-run-history-chart']");
    public ILocator RunHistoryBars => page.Locator("[data-testid='quality-run-history-bar']");
    public ILocator LowConfidenceTable => page.Locator("[data-testid='quality-low-confidence-table']");
    public ILocator LowConfidenceRows => page.Locator("[data-testid='quality-low-confidence-row']");
    public ILocator RowMarkCorrectButtons => page.Locator("[data-testid='quality-row-mark-correct-btn']");
    public ILocator RecentRunRows => page.Locator("[data-testid='quality-recent-run-row']");
    public ILocator TraceView => page.Locator("[data-testid='quality-trace-view']");
    public ILocator TraceSteps => page.Locator("[data-testid='quality-trace-step']");
    public ILocator TraceFixKnowledgeBaseButton => page.Locator("[data-testid='quality-fix-kb-btn']");
    public ILocator TraceMarkCorrectButton => page.Locator("[data-testid='quality-trace-mark-correct-btn']");

    public async Task GoToAsync() =>
        await page.GotoAsync(GlobalSetup.BaseUrl + "/quality");

    public async Task WaitForLoadAsync()
    {
        await page.WaitForURLAsync(u => u.Contains("/quality"), new() { Timeout = 15_000 });
        await page.WaitForSelectorAsync(
            "[data-testid='quality-page']",
            new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await page.WaitForSelectorAsync(
            "[data-testid='quality-run-eval-btn']",
            new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
    }

    public async Task<string?> TriggerEvalRunAsync()
    {
        var responseTask = page.WaitForResponseAsync(
            response =>
                response.Url.Contains("/portal/evals/run", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new() { Timeout = RunEvalTimeoutSeconds * 1000 });

        await RunEvalButton.ClickAsync();
        var triggerResponse = await responseTask;
        var triggerBody = await triggerResponse.TextAsync();
        if (triggerResponse.Status >= 400)
        {
            throw new InvalidOperationException(
                $"Trigger eval API failed status={triggerResponse.Status} body={triggerBody}");
        }

        string? runId = null;
        try
        {
            using var triggerJson = JsonDocument.Parse(triggerBody);
            if (triggerJson.RootElement.TryGetProperty("runId", out var runIdElement))
                runId = runIdElement.GetString();
        }
        catch
        {
            // ignore
        }

        var waitStarted = DateTime.UtcNow;
        var nextProgressLogAtSeconds = 30;
        var deadline = DateTime.UtcNow.AddSeconds(RunEvalTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var text = (await RunEvalButton.TextContentAsync())?.Trim();
            if (string.Equals(text, "Run Evaluation", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(runId))
                    await LogRunDiagnosticsAsync(runId!);
                return runId;
            }

            await Task.Delay(200);
            var elapsedSeconds = (int)(DateTime.UtcNow - waitStarted).TotalSeconds;
            if (elapsedSeconds >= nextProgressLogAtSeconds)
            {
                Console.WriteLine(
                    $"[EVAL-TRACE] waiting run completion runId={runId ?? "unknown"} elapsedSeconds={elapsedSeconds} buttonText={text}");
                nextProgressLogAtSeconds += 30;
            }
        }

        throw new TimeoutException($"Run Evaluation button did not return to idle state in {RunEvalTimeoutSeconds}s.");
    }

    public async Task<string?> FetchRunTriggerContextJsonAsync(string runId)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('sl_access') || ''");
        var headers = string.IsNullOrWhiteSpace(token)
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
        var detailResponse = await page.Context.APIRequest.GetAsync(
            $"{GlobalSetup.BaseUrl}/portal/evals/runs/{runId}",
            new() { Headers = headers });
        if (!detailResponse.Ok)
            return null;
        var detailBody = await detailResponse.TextAsync();
        using var detailDoc = JsonDocument.Parse(detailBody);
        if (!detailDoc.RootElement.TryGetProperty("run", out var runElement))
            return null;
        if (!runElement.TryGetProperty("configSnapshotJson", out var configSnapshotElement))
            return null;
        var configSnapshotJson = configSnapshotElement.GetString();
        if (string.IsNullOrWhiteSpace(configSnapshotJson))
            return null;
        using var configDoc = JsonDocument.Parse(configSnapshotJson);
        if (!configDoc.RootElement.TryGetProperty("triggerContext", out var triggerContext))
            return null;
        return triggerContext.GetRawText();
    }

    private async Task LogRunDiagnosticsAsync(string runId)
    {
        try
        {
            var token = await page.EvaluateAsync<string>("() => localStorage.getItem('sl_access') || ''");
            var headers = string.IsNullOrWhiteSpace(token)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
            var detailResponse = await page.Context.APIRequest.GetAsync(
                $"{GlobalSetup.BaseUrl}/portal/evals/runs/{runId}",
                new() { Headers = headers });
            var detailBody = await detailResponse.TextAsync();
            if (!detailResponse.Ok)
            {
                Console.WriteLine(
                    $"[EVAL-TRACE] run detail fetch failed runId={runId} status={detailResponse.Status} body={detailBody}");
                return;
            }

            using var detailDoc = JsonDocument.Parse(detailBody);
            if (!detailDoc.RootElement.TryGetProperty("run", out var runElement))
                return;
            if (!runElement.TryGetProperty("configSnapshotJson", out var configSnapshotElement))
                return;

            var configSnapshotJson = configSnapshotElement.GetString();
            if (string.IsNullOrWhiteSpace(configSnapshotJson))
                return;

            using var configDoc = JsonDocument.Parse(configSnapshotJson);
            if (!configDoc.RootElement.TryGetProperty("triggerContext", out var triggerContext))
                return;

            Console.WriteLine($"[EVAL-TRACE] runId={runId} triggerContext={triggerContext.GetRawText()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EVAL-TRACE] failed to emit run diagnostics runId={runId} error={ex.Message}");
        }
    }

    private static int ResolveRunEvalTimeoutSeconds()
    {
        var explicitTimeout = Environment.GetEnvironmentVariable("E2E_RUN_EVAL_TIMEOUT_SECONDS");
        if (!string.IsNullOrWhiteSpace(explicitTimeout)
            && int.TryParse(explicitTimeout, out var parsed)
            && parsed > 0)
        {
            return Math.Clamp(parsed, 30, 7_200);
        }

        var realEvalSetting = Environment.GetEnvironmentVariable("E2E_REQUIRE_REAL_EVAL");
        var realEvalEnabled = !string.Equals(realEvalSetting?.Trim(), "0", StringComparison.Ordinal);
        return realEvalEnabled ? 1_800 : 360;
    }
}
